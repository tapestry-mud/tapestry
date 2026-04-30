// src/Tapestry.Engine/Prompt/PromptRenderer.cs
using System.Text.RegularExpressions;
using Tapestry.Engine.Economy;

namespace Tapestry.Engine.Prompt;

public partial class PromptRenderer
{
    public const string DefaultTemplate =
        "<hp>[HP]: {hp}/{maxhp}</hp> | <mana>[Mana]: {mana}/{maxmana}</mana> | <mv>[Mv]: {mv}/{maxmv}</mv>> ";

    private static readonly Regex TokenPattern = TokenRegex();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex TokenRegex();

    public string Render(string template, Entity entity)
    {
        return TokenPattern.Replace(template, match =>
        {
            var token = match.Groups[1].Value.ToLowerInvariant();
            return ResolveToken(token, entity) ?? "";
        });
    }

    private static string? ResolveToken(string token, Entity entity)
    {
        return token switch
        {
            "hp" => entity.Stats.Hp.ToString(),
            "maxhp" => entity.Stats.MaxHp.ToString(),
            "mana" => entity.Stats.Resource.ToString(),
            "maxmana" => entity.Stats.MaxResource.ToString(),
            "mv" => entity.Stats.Movement.ToString(),
            "maxmv" => entity.Stats.MaxMovement.ToString(),
            "gold" => entity.GetProperty<int>(CurrencyProperties.Gold).ToString(),
            _ => null
        };
    }
}
