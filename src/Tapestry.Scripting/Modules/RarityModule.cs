using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine.Color;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Registration;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class RarityModule : IJintApiModule
{
    private readonly RarityRegistry _registry;
    private readonly ThemeRegistry _themeRegistry;
    private readonly RegistrationPolicy _registrationPolicy;

    public RarityModule(RarityRegistry registry, ThemeRegistry themeRegistry, RegistrationPolicy registrationPolicy)
    {
        _registry = registry;
        _themeRegistry = themeRegistry;
        _registrationPolicy = registrationPolicy;
    }

    public string Namespace => "rarity";

    public object Build(JintEngine engine)
    {
        return new
        {
            register = new Action<JsValue>(def =>
            {
                var obj = (ObjectInstance)def;
                var key = obj.Get("key").ToString();
                var order = (int)(double)obj.Get("order").ToObject()!;
                var visible = (bool)obj.Get("visible").ToObject()!;

                string? displayText = null;
                var dtVal = obj.Get("displayText");
                if (dtVal.Type != Types.Undefined && dtVal.Type != Types.Null)
                {
                    displayText = dtVal.ToString();
                }

                (string Left, string Right)? decorators = null;
                var decVal = obj.Get("decorators");
                if (decVal.Type != Types.Undefined && decVal.Type != Types.Null)
                {
                    var decObj = (ObjectInstance)decVal;
                    decorators = (decObj.Get("left").ToString(), decObj.Get("right").ToString());
                }

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
                // so two packs registering the same rarity key is a boot error unless one
                // declares { override: true } + a dependency edge on the owner.
                _registrationPolicy.Record(new RegistrationCandidate(
                    Kind: "rarity",
                    Name: key,
                    Owner: packName,
                    IsOverride: isOverride,
                    Commit: () =>
                    {
                        _registry.Register(new RarityTierDefinition(key, order, displayText, decorators, color, visible));
                        _themeRegistry.Register($"item.{key}", new ThemeEntry { Fg = color, Html = html });
                    },
                    SourceFile: sourceFile,
                    Line: 0));
            }),

            format = new Func<string, string>(rarityKey => _registry.Format(rarityKey)),

            formatInline = new Func<string?, string>(rarityKey => _registry.FormatInline(rarityKey)),

            getTier = new Func<string, object?>(key =>
            {
                var tier = _registry.GetTier(key);
                if (tier == null) { return null; }
                return new
                {
                    key = tier.Key,
                    order = tier.Order,
                    displayText = tier.DisplayText,
                    color = tier.Color,
                    visible = tier.Visible
                };
            }),

            tagWidth = new Func<int>(() => _registry.TagWidth)
        };
    }
}
