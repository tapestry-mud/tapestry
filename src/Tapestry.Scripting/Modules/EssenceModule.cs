using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine.Color;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Registration;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class EssenceModule : IJintApiModule
{
    private readonly EssenceRegistry _registry;
    private readonly ThemeRegistry _themeRegistry;
    private readonly RegistrationPolicy _registrationPolicy;

    public EssenceModule(EssenceRegistry registry, ThemeRegistry themeRegistry, RegistrationPolicy registrationPolicy)
    {
        _registry = registry;
        _themeRegistry = themeRegistry;
        _registrationPolicy = registrationPolicy;
    }

    public string Namespace => "essence";

    public object Build(JintEngine engine)
    {
        return new
        {
            register = new Action<JsValue>(def =>
            {
                var obj = (ObjectInstance)def;
                var key = obj.Get("key").ToString();
                var glyph = obj.Get("glyph").ToString();
                var color = obj.Get("color").ToString();

                string? html = null;
                var htmlVal = obj.Get("html");
                if (htmlVal.Type != Types.Undefined && htmlVal.Type != Types.Null)
                {
                    html = htmlVal.ToString();
                }

                // Jint 4.7.1 has no IsBoolean; a missing JS field marshals to CLR null. Read via Type==Boolean.
                var overrideVal = obj.Get("override");
                bool isOverride = overrideVal.Type == Types.Boolean && (bool)overrideVal.ToObject()!;

                var packName = engine.GetValue("__currentPack").ToString();

                var sourceFileVal = engine.GetValue("__currentSource");
                var sourceFile = (sourceFileVal.Type != Types.Undefined && sourceFileVal.Type != Types.Null)
                    ? sourceFileVal.ToString()
                    : "";

                // Declarative: accumulate a candidate. The real Register (and the theme-tag
                // side-effect, which piggybacks the winner) replays at Resolve() (the seal),
                // so two packs registering the same essence key is a boot error unless one
                // declares { override: true } + a dependency edge on the owner.
                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "essence",
                    Name: key,
                    Owner: packName,
                    IsOverride: isOverride,
                    Commit: () =>
                    {
                        _registry.Register(new EssenceDefinition(key, glyph, color));
                        _themeRegistry.Register($"essence.{key}", new ThemeEntry { Fg = color, Html = html });
                    },
                    SourceFile: sourceFile,
                    Line: 0));
            }),

            format = new Func<string?, string>(essenceKey => _registry.Format(essenceKey)),

            getEssence = new Func<string, object?>(key =>
            {
                var essence = _registry.GetEssence(key);
                if (essence == null) { return null; }
                return new { key = essence.Key, glyph = essence.Glyph, color = essence.Color };
            })
        };
    }
}
