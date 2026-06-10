using System.Dynamic;
using Jint;
using Microsoft.Extensions.Logging;

using JintEngine = Jint.Engine;

namespace Tapestry.Scripting;

public class JintRuntime
{
    private readonly IEnumerable<IJintApiModule> _modules;
    private readonly JintEngine _engine;
    private readonly ILogger<JintRuntime> _logger;

    // Boot-time guard against running the same physical script file twice. The pack `scripts:`
    // glob and the quest `script:` field (QuestStartupModule) are independent boot subsystems
    // that both execute JS files; a file covered by both used to run twice, re-firing every
    // registerX in it. The old last-write-wins registries absorbed that silently; the
    // RegistrationPolicy turns it into a "two packs register X" collision. Dedupe by full path.
    private readonly HashSet<string> _executedFiles = new(StringComparer.OrdinalIgnoreCase);

    public JintRuntime(IEnumerable<IJintApiModule> modules, ILogger<JintRuntime> logger,
        MobInvocationBudget? mobBudget = null)
    {
        _modules = modules;
        _logger = logger;
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
    /// Execute a script without a pack name. Convenience for tests.
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

    private void SetupApi()
    {
        var tapestry = new ExpandoObject() as IDictionary<string, object?>;
        foreach (var module in _modules)
        {
            tapestry[module.Namespace] = module.Build(_engine);
        }
        _engine.SetValue("tapestry", tapestry);
    }
}
