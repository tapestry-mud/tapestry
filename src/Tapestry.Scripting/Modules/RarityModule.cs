using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Tapestry.Engine.Color;
using Tapestry.Engine.Inventory;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class RarityModule : IJintApiModule
{
    private readonly RarityRegistry _registry;
    private readonly ThemeRegistry _themeRegistry;

    public RarityModule(RarityRegistry registry, ThemeRegistry themeRegistry)
    {
        _registry = registry;
        _themeRegistry = themeRegistry;
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

                _registry.Register(new RarityTierDefinition(key, order, displayText, decorators, color, visible));

                string? html = null;
                var htmlVal = obj.Get("html");
                if (htmlVal.Type != Types.Undefined && htmlVal.Type != Types.Null)
                {
                    html = htmlVal.ToString();
                }

                _themeRegistry.Register($"item.{key}", new ThemeEntry { Fg = color, Html = html });
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
