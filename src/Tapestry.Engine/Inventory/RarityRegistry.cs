namespace Tapestry.Engine.Inventory;

public class RarityRegistry
{
    private readonly List<RarityTierDefinition> _tiers = new();
    private readonly Dictionary<string, RarityTierDefinition> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private int _maxTagWidth = -1;
    private int _maxDisplayTextWidth = 0;

    public void Register(RarityTierDefinition tier)
    {
        _byKey[tier.Key] = tier;
        if (!_tiers.Any(t => t.Key.Equals(tier.Key, StringComparison.OrdinalIgnoreCase)))
        {
            _tiers.Add(tier);
        }
        _maxTagWidth = -1;
    }

    public RarityTierDefinition? GetTier(string key) => _byKey.GetValueOrDefault(key);

    public int TagWidth
    {
        get
        {
            EnsureWidthComputed();
            return _maxTagWidth;
        }
    }

    public string FormatInline(string? rarityKey)
    {
        if (rarityKey == null) { return string.Empty; }
        var tier = _byKey.GetValueOrDefault(rarityKey);
        if (tier == null || !tier.Visible || tier.DisplayText == null || tier.Decorators == null)
        {
            return string.Empty;
        }
        var raw = tier.Decorators.Value.Left + tier.DisplayText + tier.Decorators.Value.Right;
        return $"<item.{tier.Key}>{raw}</item.{tier.Key}>";
    }

    public string Format(string? rarityKey)
    {
        EnsureWidthComputed();
        if (_maxTagWidth == 0) { return string.Empty; }

        var tier = rarityKey != null ? _byKey.GetValueOrDefault(rarityKey) : null;

        if (tier == null || !tier.Visible || tier.DisplayText == null || tier.Decorators == null)
        {
            return new string(' ', _maxTagWidth);
        }

        var paddedText = CenterPad(tier.DisplayText, _maxDisplayTextWidth);
        var raw = tier.Decorators.Value.Left + paddedText + tier.Decorators.Value.Right;
        return $"<item.{tier.Key}>{raw}</item.{tier.Key}>";
    }

    private void EnsureWidthComputed()
    {
        if (_maxTagWidth >= 0) { return; }

        var visible = _tiers
            .Where(t => t.Visible && t.DisplayText != null && t.Decorators != null)
            .ToList();

        if (visible.Count == 0)
        {
            _maxTagWidth = 0;
            _maxDisplayTextWidth = 0;
            return;
        }

        _maxDisplayTextWidth = visible.Max(t => t.DisplayText!.Length);
        _maxTagWidth = visible.Max(t =>
            t.Decorators!.Value.Left.Length + _maxDisplayTextWidth + t.Decorators!.Value.Right.Length);
    }

    private static string CenterPad(string text, int width)
    {
        if (text.Length >= width) { return text; }
        var total = width - text.Length;
        var leftPad = total / 2;
        var rightPad = total - leftPad;
        return new string(' ', leftPad) + text + new string(' ', rightPad);
    }
}
