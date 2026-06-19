using System.Collections.Generic;
using System.Dynamic;
using Jint;
using Jint.Native;
using Microsoft.Extensions.Logging;

using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

public class JintRuntime
{
    private readonly IEnumerable<IJintApiModule> _modules;
    private readonly JintEngine _engine;
    private readonly ILogger<JintRuntime> _logger;
    private readonly Interop.TapestryModuleLoader? _loader;

    // Boot-time guard against running the same physical script file twice. The pack `scripts:`
    // glob and the quest `script:` field (QuestStartupModule) are independent boot subsystems
    // that both execute JS files; a file covered by both used to run twice, re-firing every
    // registerX in it. The old last-write-wins registries absorbed that silently; the
    // RegistrationPolicy turns it into a "two packs register X" collision. Dedupe by full path.
    private readonly HashSet<string> _executedFiles = new(StringComparer.OrdinalIgnoreCase);

    public JintRuntime(IEnumerable<IJintApiModule> modules, ILogger<JintRuntime> logger,
        MobInvocationBudget? mobBudget = null, Interop.TapestryModuleLoader? loader = null)
    {
        _modules = modules;
        _logger = logger;
        _loader = loader;
        _engine = new JintEngine(options =>
        {
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
            options.LimitRecursion(100);
            options.LimitMemory(50_000_000);
            options.Strict();
            if (mobBudget != null)
            {
                options.Constraints.Constraints.Add(mobBudget);
            }
            if (loader != null)
            {
                options.EnableModules(loader);
            }
        });

        SetupApi();
    }

    public void Execute(string script, string packName)
    {
        _engine.SetValue("__currentPack", packName);
        _engine.SetValue("__currentSource", "");
        _engine.Execute(script);
    }

    public void Execute(string script, string packName, string sourceFile)
    {
        _engine.SetValue("__currentPack", packName);
        _engine.SetValue("__currentSource", sourceFile);
        _engine.Execute(script, source: sourceFile);
    }

    /// <summary>
    /// Execute a script without setting pack/source attribution. TESTS ONLY -- do NOT use for
    /// pack execution: it leaves __currentPack/__currentSource at their prior values, so any
    /// registration (e.g. registerScript) records a stale/blank owner. Pack content must run
    /// through the attributed Execute(script, packName, sourceFile) overload (the scripts: glob).
    /// </summary>
    public void Execute(string script)
    {
        _engine.Execute(script);
    }

    /// <summary>
    /// Records that a script file (by absolute path) was executed this boot, building the
    /// ledger of which files the pack <c>scripts:</c> glob loaded. Path is normalised
    /// (<see cref="Path.GetFullPath(string)"/>) so glob-relative and other forms of the same
    /// file collapse to one key.
    ///
    /// CONSUMER: QuestStartupModule's quest-script coverage assertion
    /// (QuestScriptCoverageVerifier) reads this ledger via <see cref="HasExecutedFile"/> to
    /// confirm every quest <c>script:</c> was actually loaded by the glob. Do not remove the
    /// glob's call to this method as "dead" -- that coverage check depends on it.
    ///
    /// The bool return (true on first sight, false on repeat) is currently unused by every
    /// caller; it is kept to preserve the idempotence/path-normalisation contract pinned by
    /// JintRuntimeTests.MarkFileExecuted_IsIdempotent_AndPathNormalized.
    /// </summary>
    public bool MarkFileExecuted(string absolutePath)
    {
        return _executedFiles.Add(Path.GetFullPath(absolutePath));
    }

    /// <summary>True if the given file path was already executed this boot.</summary>
    public bool HasExecutedFile(string absolutePath)
    {
        return _executedFiles.Contains(Path.GetFullPath(absolutePath));
    }

    /// <summary>
    /// Evaluate an expression and return the result as a CLR object (or null).
    /// </summary>
    public object? Evaluate(string expression)
    {
        var result = _engine.Evaluate(expression);
        if (result == null || result.IsNull() || result.IsUndefined())
        {
            return null;
        }
        return result.ToObject();
    }

    /// <summary>
    /// No-op — API is built in the constructor. Kept for explicit-initialization tests.
    /// </summary>
    public void Initialize()
    {
    }

    public string ModuleKey(string ns, string relFile) => _loader!.ModuleKey(ns, relFile);

    public void ImportModule(string moduleKey) => _engine.Modules.Import(moduleKey);

    /// <summary>The pack namespace of the lexically active module (model A attribution), or null
    /// when no pack module is active (engine builder module, or a legacy Execute script).</summary>
    public string? GetActivePack() =>
        Interop.TapestryModuleLoader.PackOf(JintActiveModule.ActiveLocation(_engine));

    /// <summary>TEST-ONLY: the ESM module loader (for the EsmTest harness). Null only on legacy-only runtimes.</summary>
    internal Interop.TapestryModuleLoader? Loader => _loader;

    /// <summary>TEST-ONLY: import a module and read one of its named exports (for EsmTest.Eval).</summary>
    internal JsValue? ImportModuleAndGet(string moduleKey, string exportName)
        => _engine.Modules.Import(moduleKey).Get(exportName);

    /// <summary>TEST-ONLY raw-JsValue evaluate for the fixture (the public object? Evaluate stays).</summary>
    internal JsValue EvaluateRaw(string expression) => _engine.Evaluate(expression);

    private void SetupApi()
    {
        // Build each module EXACTLY once; both surfaces share the built objects (AbilitiesModule.Build
        // subscribes to an event, so a second build double-subscribes; PacksModule/RespondModule re-run
        // their engine.Evaluate dance).
        var built = new Dictionary<string, object?>();
        foreach (var module in _modules)
        {
            built[module.Namespace] = module.Build(_engine);
        }

        // Legacy surface: the global `tapestry` object (used by legacy Execute packs).
        var tapestry = new ExpandoObject() as IDictionary<string, object?>;
        foreach (var entry in built)
        {
            tapestry[entry.Key] = entry.Value;
        }
        _engine.SetValue("tapestry", tapestry);

        // ESM surface: one shared `@tapestry/engine` builder module (no JS top-level code, so it is
        // never the "active module" during a pack API call - which is what makes GetActivePack() see
        // the calling pack). Each namespace is a named export. Only when modules are enabled.
        if (_loader != null)
        {
            _engine.Modules.Add("@tapestry/engine", builder =>
            {
                foreach (var entry in built)
                {
                    var value = entry.Value is JsValue jsv
                        ? jsv
                        : JsValue.FromObject(_engine, entry.Value);
                    builder.ExportValue(entry.Key, value);
                }
            });
        }
    }
}
