using System.Text.RegularExpressions;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tapestry.Engine;
using Tapestry.Scripting.Interop;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class PacksModule : IJintApiModule
{
    private static readonly Regex ExportNamePattern = new(@"^[a-zA-Z_$][a-zA-Z0-9_$]*$", RegexOptions.Compiled);

    private readonly IServiceProvider _services;
    private readonly PackExportRegistry _exports;
    private readonly PackDependencyGraph _graph;
    private readonly ILogger<PacksModule> _logger;

    public PacksModule(
        IServiceProvider services,
        PackExportRegistry exports,
        PackDependencyGraph graph,
        ILogger<PacksModule> logger)
    {
        _services = services;
        _exports = exports;
        _graph = graph;
        _logger = logger;
    }

    public string Namespace => "packs";

    public object Build(JintEngine engine)
    {
        // Jint's DelegateWrapper binds JS call args strictly positionally to fixed C#
        // parameters and does not collect trailing args into an array. So `call` (which is
        // variadic: call(pack, name, ...args)) is exposed through a JS shim that gathers
        // its arguments into a real JS array and hands that single array to the C# delegate
        // (which Jint then marshals to JsValue[]). Same temp-global pattern as RespondModule.
        var listFunc = new Func<object[]>(ListPacks);

        engine.SetValue("__packsList__", listFunc);
        engine.SetValue("__packsExport__",
            new Action<string, JsValue, JsValue>((name, handler, metadata) =>
                Export(engine, name, handler, metadata)));
        engine.SetValue("__packsCall__",
            new Func<JsValue, JsValue>(argsArray => Call(engine, argsArray)));
        engine.SetValue("__packsHas__",
            new Func<string, JsValue, bool>((pack, name) => Has(engine, pack, name)));
        engine.SetValue("__packsGetExportRegistry__", new Func<object[]>(GetExportRegistry));

        var packs = engine.Evaluate("""
            (function () {
                var _list = __packsList__;
                var _export = __packsExport__;
                var _call = __packsCall__;
                var _has = __packsHas__;
                var _getReg = __packsGetExportRegistry__;
                return {
                    list: function () { return _list(); },
                    getAll: function () { return _list(); },
                    export: function (name, handler, metadata) { return _export(name, handler, metadata); },
                    call: function (pack, name) {
                        return _call(Array.prototype.slice.call(arguments));
                    },
                    has: function (pack, name) { return _has(pack, name); },
                    getExportRegistry: function () { return _getReg(); }
                };
            })()
            """);

        engine.SetValue("__packsList__", JsValue.Null);
        engine.SetValue("__packsExport__", JsValue.Null);
        engine.SetValue("__packsCall__", JsValue.Null);
        engine.SetValue("__packsHas__", JsValue.Null);
        engine.SetValue("__packsGetExportRegistry__", JsValue.Null);

        return packs;
    }

    // ---- interop ----

    private void Export(JintEngine engine, string name, JsValue handler, JsValue metadata)
    {
        if (string.IsNullOrEmpty(name) || !ExportNamePattern.IsMatch(name))
        {
            throw new InteropException(
                $"Invalid export name '{name}'. Use a JS identifier (camelCase), e.g. 'getHungerTier'.");
        }
        if (handler is null || handler.Type != Types.Object || handler is not Jint.Native.Function.Function)
        {
            throw new InteropException($"Export '{name}' handler must be a function.");
        }

        var pack = PackLoader.PackNamespace(engine.GetValue("__currentPack").ToString());

        var meta = metadata as ObjectInstance;
        var kind = GetString(meta, "kind", "query");
        var description = GetString(meta, "description", "");
        var returns = GetString(meta, "returns", "");
        var paramsList = GetParams(meta);
        var appliesTo = GetStringArray(meta, "appliesTo", new[] { "all" });

        _exports.Register(new ExportEntry(pack, name, handler, description, paramsList, returns, kind, appliesTo));
    }

    private JsValue Call(JintEngine engine, JsValue argsArrayVal)
    {
        // The JS shim (see Build) gathers call(pack, name, ...args) into one JS array and hands
        // it over as a single JsValue. We unpack the elements here rather than declaring a CLR
        // `JsValue[]` parameter: Jint's Array -> JsValue[] marshalling round-trips each element
        // and throws "No valid constructors found for type JsValue" on a plain-object element
        // (primitives survive). Reading the array's indexed values keeps each element a JsValue
        // -- objects and nested arrays included -- exactly as the export expects.
        if (argsArrayVal is not ObjectInstance argsArray)
        {
            throw new InteropException("tapestry.packs.call requires (pack, exportName, ...args).");
        }
        var length = (int)TypeConverter.ToNumber(argsArray.Get("length"));
        var allArgs = new JsValue[length];
        for (var i = 0; i < length; i++)
        {
            allArgs[i] = argsArray.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // First two entries are pack + export name; the rest are the export's arguments.
        if (allArgs.Length < 2)
        {
            throw new InteropException("tapestry.packs.call requires (pack, exportName, ...args).");
        }
        var pack = allArgs[0].ToString();
        var name = allArgs[1].ToString();
        var args = allArgs.Length > 2 ? allArgs[2..] : Array.Empty<JsValue>();

        var caller = PackLoader.PackNamespace(engine.GetValue("__currentPack").ToString());
        var target = PackLoader.PackNamespace(pack);

        EnforceEdge(caller, target);

        if (!_exports.TryResolve(target, name, out var entry))
        {
            throw new InteropException(
                $"Pack '{target}' has no export named '{name}' (called by '{caller}').");
        }

        using var activity = TapestryTracing.Source.StartActivity("interop.call");
        activity?.SetTag("interop.caller", caller);
        activity?.SetTag("interop.target", target);
        activity?.SetTag("interop.export", name);

        try
        {
            // Run the export body attributed to ITS pack, so a nested tapestry.packs.call
            // from within is gated against the export's edges, not the stale outer caller's.
            return engine.InvokeAsPack(entry.Pack, entry.Handler, null, args);
        }
        catch (JavaScriptException jsEx)
        {
            activity?.SetTag("error", true);
            _logger.LogError(jsEx,
                "Interop export '{Export}' provided by '{Target}' faulted (called by '{Caller}')",
                name, target, caller);
            throw;
        }
    }

    private bool Has(JintEngine engine, string pack, JsValue name)
    {
        var caller = PackLoader.PackNamespace(engine.GetValue("__currentPack").ToString());
        var target = PackLoader.PackNamespace(pack);

        EnforceEdge(caller, target);

        if (name.Type == Types.Undefined || name.Type == Types.Null)
        {
            return _graph.IsLoaded(target);
        }
        return _exports.Has(target, name.ToString());
    }

    private object[] GetExportRegistry() =>
        _exports.GetAll()
            .Select(e => (object)new
            {
                name = e.Name,
                pack = e.Pack,
                description = e.Description,
                @params = e.Params.ToArray(),
                returns = e.Returns,
                kind = e.Kind,
                appliesTo = e.AppliesTo.ToArray(),
            })
            .ToArray();

    private void EnforceEdge(string caller, string target)
    {
        if (string.Equals(caller, target, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!_graph.DeclaresEdge(caller, target))
        {
            throw new InteropException(
                $"Pack '{caller}' has no declared dependency on '{target}'; " +
                $"add it to `dependencies` or `optional_dependencies`.");
        }
    }

    // ---- metadata parsing helpers ----

    private static string GetString(ObjectInstance? meta, string key, string fallback)
    {
        if (meta is null) { return fallback; }
        var v = meta.Get(key);
        return (v.Type != Types.Undefined && v.Type != Types.Null) ? v.ToString() : fallback;
    }

    private static IReadOnlyList<string> GetStringArray(ObjectInstance? meta, string key, string[] fallback)
    {
        if (meta is null) { return fallback; }
        if (meta.Get(key) is JsArray arr && arr.Length > 0)
        {
            var result = new string[arr.Length];
            for (uint i = 0; i < arr.Length; i++) { result[i] = arr[i].ToString(); }
            return result;
        }
        return fallback;
    }

    private static IReadOnlyList<string> GetParams(ObjectInstance? meta)
    {
        if (meta is null || meta.Get("params") is not JsArray arr) { return Array.Empty<string>(); }
        var result = new List<string>();
        for (uint i = 0; i < arr.Length; i++)
        {
            if (arr[i] is ObjectInstance p)
            {
                var pname = p.Get("name");
                var ptype = p.Get("type");
                var n = pname.Type != Types.Undefined ? pname.ToString() : "arg";
                var t = ptype.Type != Types.Undefined ? ptype.ToString() : "any";
                result.Add($"{n}:{t}");
            }
        }
        return result;
    }

    // ---- existing pack listing (unchanged) ----

    private object[] ListPacks()
    {
        return _services.GetRequiredService<PackLoader>().LoadedPacks
            .OrderBy(p => p.LoadOrder)
            .Select(p => (object)new
            {
                name = PackLoader.PackNamespace(p.Name),
                displayName = p.DisplayName,
                version = p.Version,
                description = p.Description,
                author = p.Author,
                copyright = p.Copyright,
                website = p.Website,
                license = p.License
            })
            .ToArray();
    }
}
