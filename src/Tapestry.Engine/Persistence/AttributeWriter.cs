using System.Globalization;
using Tapestry.Engine.Tags;

namespace Tapestry.Engine.Persistence;

public sealed record AttributeWriteResult(bool Ok, string Message);

/// <summary>
/// Registry-driven attribute writer: resolves an attribute name across
/// PropertyRegistry then TagRegistry, coerces raw tokens to the declared type,
/// validates type/applies-to/constraints, and writes to a live entity.
/// "Declared ⟺ settable": only registry-declared attributes can be written here.
/// </summary>
public sealed class AttributeWriter
{
    private readonly PropertyRegistry _properties;
    private readonly TagRegistry _tags;

    public AttributeWriter(PropertyRegistry properties, TagRegistry tags)
    {
        _properties = properties;
        _tags = tags;
    }

    public AttributeWriteResult Write(IAttributeTarget target, string attr, IReadOnlyList<string> valueTokens)
    {
        var prop = FindProperty(attr, out var ambiguityError);
        if (prop != null) { return WriteProperty(target, prop, valueTokens); }
        if (ambiguityError != null) { return new AttributeWriteResult(false, ambiguityError); }

        var tag = FindTag(attr);
        if (tag != null) { return WriteTag(target, tag, valueTokens); }

        return new AttributeWriteResult(false,
            $"Unknown attribute '{attr}' on {target.Name}. Try `set {BaseType(target.Type)} ?`.");
    }

    /// <summary>Value-omitted read: echo current value + metadata + usage (the per-attribute read).</summary>
    public AttributeWriteResult Describe(IAttributeTarget target, string attr)
    {
        var prop = FindProperty(attr, out var ambiguityError);
        if (prop != null)
        {
            if (!prop.IsAdminSettable)
            {
                return new AttributeWriteResult(false, $"{prop.Name} is engine-managed and can't be set.");
            }
            var current = target.GetRawProperty(prop.Name);
            var currentStr = current switch
            {
                null => "(unset)",
                bool b => b ? "true" : "false",
                _ => current.ToString() ?? "(unset)"
            };
            var applies = prop.AppliesTo == null ? "all" : string.Join("/", prop.AppliesTo);
            var typeStr = ValueTypeName(prop.ValueType);
            var hint = prop.ValueType == PropertyValueType.Bool ? "<true|false>" : "<value>";
            return new AttributeWriteResult(true,
                $"{prop.Name} on {target.Name} = {currentStr} ({typeStr}, applies to {applies}). " +
                $"Usage: set {BaseType(target.Type)} {prop.Name} <target> {hint}");
        }

        if (ambiguityError != null) { return new AttributeWriteResult(false, ambiguityError); }

        var tag = FindTag(attr);
        if (tag != null)
        {
            var present = target.HasTag(tag.Name) ? "true" : "false";
            var applies = string.Join("/", tag.AppliesTo);
            return new AttributeWriteResult(true,
                $"{tag.Name} on {target.Name} = {present} (tag, applies to {applies}). " +
                $"Usage: set {BaseType(target.Type)} {tag.Name} <target> <on|off>");
        }

        return new AttributeWriteResult(false,
            $"Unknown attribute '{attr}' on {target.Name}. Try `set {BaseType(target.Type)} ?`.");
    }

    // Admins address attributes by bare declared name with NO pack context (the `set` command is
    // engine-internal, not pack JS). We route through the registry's ambiguity-aware resolver so a
    // unique bare name resolves, a pack-qualified "pack:name" resolves via exact key, and an
    // ambiguous bare name is REJECTED with a located diagnostic instead of silently first-winning.
    // ambiguityError is set (and the entry null) only when two-or-more packs declare the same name.
    private PropertyRegistryEntry? FindProperty(string attr, out string? ambiguityError)
    {
        ambiguityError = null;
        var resolution = _properties.ResolveForAdmin(attr);
        switch (resolution.Status)
        {
            case PropertyResolutionStatus.Found:
            {
                return resolution.Entry;
            }
            case PropertyResolutionStatus.Ambiguous:
            {
                ambiguityError =
                    $"Property '{attr}' is ambiguous: declared by {string.Join(", ", resolution.Owners)}. " +
                    $"Qualify it as {resolution.Owners[0]}:{attr}.";
                return null;
            }
            default:
            {
                return null;
            }
        }
    }

    private TagRegistryEntry? FindTag(string attr) =>
        _tags.GetAll().FirstOrDefault(e => e.Name.Equals(attr, StringComparison.OrdinalIgnoreCase));

    private static string BaseType(string entityType)
    {
        var idx = entityType.IndexOf(':');
        return idx >= 0 ? entityType[..idx] : entityType;
    }

    private AttributeWriteResult WriteProperty(IAttributeTarget target, PropertyRegistryEntry entry, IReadOnlyList<string> tokens)
    {
        if (!entry.IsAdminSettable)
        {
            return new AttributeWriteResult(false, $"{entry.Name} is engine-managed and can't be set.");
        }
        if (!entry.AppliesToType(BaseType(target.Type)))
        {
            var applies = entry.AppliesTo == null ? "all" : string.Join("/", entry.AppliesTo);
            return new AttributeWriteResult(false,
                $"Cannot set {entry.Name} on {target.Name} - that attribute applies to {applies} only.");
        }
        if (tokens.Count == 0)
        {
            return new AttributeWriteResult(false,
                $"Usage: set {BaseType(target.Type)} {entry.Name} <target> <value>");
        }
        if (!TryCoerce(entry.ValueType, tokens, out var coerced, out var coerceError))
        {
            return new AttributeWriteResult(false, coerceError);
        }
        if (!CheckConstraints(entry, coerced, out var constraintError))
        {
            return new AttributeWriteResult(false, constraintError);
        }
        target.SetProperty(entry.Name, coerced);
        return new AttributeWriteResult(true, $"{target.Name}'s {entry.Name} set to {coerced}.");
    }

    private AttributeWriteResult WriteTag(IAttributeTarget target, TagRegistryEntry entry, IReadOnlyList<string> tokens)
    {
        if (!entry.AppliesToType(BaseType(target.Type)))
        {
            var applies = entry.AppliesTo.Count == 0 ? "all" : string.Join("/", entry.AppliesTo);
            return new AttributeWriteResult(false,
                $"Cannot set {entry.Name} on {target.Name} - that tag applies to {applies} only.");
        }
        if (tokens.Count == 0 || !TryParseBool(tokens[0], out var on))
        {
            return new AttributeWriteResult(false,
                $"Usage: set {BaseType(target.Type)} {entry.Name} <target> <on|off>");
        }
        if (on) { target.AddTag(entry.Name); }
        else { target.RemoveTag(entry.Name); }
        return new AttributeWriteResult(true,
            $"{entry.Name} {(on ? "set on" : "cleared on")} {target.Name}.");
    }

    private static bool TryCoerce(PropertyValueType type, IReadOnlyList<string> tokens, out object? value, out string error)
    {
        value = null;
        error = "";
        switch (type)
        {
            case PropertyValueType.String:
                value = string.Join(" ", tokens);
                return true;
            case PropertyValueType.Int:
                if (int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; }
                error = $"Expected a whole number, got '{tokens[0]}'.";
                return false;
            case PropertyValueType.Long:
                if (long.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) { value = l; return true; }
                error = $"Expected a whole number, got '{tokens[0]}'.";
                return false;
            case PropertyValueType.Double:
                if (double.TryParse(tokens[0], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
                error = $"Expected a number, got '{tokens[0]}'.";
                return false;
            case PropertyValueType.Bool:
                if (TryParseBool(tokens[0], out var b)) { value = b; return true; }
                error = $"Expected on/off (or true/false), got '{tokens[0]}'.";
                return false;
            default:
                error = $"Cannot set {ValueTypeName(type)} attributes from the command line.";
                return false;
        }
    }

    private static bool CheckConstraints(PropertyRegistryEntry entry, object? coerced, out string error)
    {
        error = "";
        if (entry.EnumValues != null && entry.EnumValues.Count > 0)
        {
            var token = coerced?.ToString() ?? "";
            if (!entry.EnumValues.Contains(token))
            {
                error = $"Value must be one of: {string.Join(", ", entry.EnumValues)}.";
                return false;
            }
        }
        if ((entry.Min != null || entry.Max != null) && TryAsDouble(coerced, out var num))
        {
            if (entry.Min != null && num < entry.Min.Value)
            {
                error = $"Value must be >= {entry.Min.Value}.";
                return false;
            }
            if (entry.Max != null && num > entry.Max.Value)
            {
                error = $"Value must be <= {entry.Max.Value}.";
                return false;
            }
        }
        return true;
    }

    private static bool TryAsDouble(object? v, out double d)
    {
        switch (v)
        {
            case int i: d = i; return true;
            case long l: d = l; return true;
            case double dd: d = dd; return true;
            default: d = 0; return false;
        }
    }

    private static bool TryParseBool(string token, out bool value)
    {
        switch (token.ToLowerInvariant())
        {
            case "on": case "true": case "yes": case "1": value = true; return true;
            case "off": case "false": case "no": case "0": value = false; return true;
            default: value = false; return false;
        }
    }

    public static string ValueTypeName(PropertyValueType type) => type switch
    {
        PropertyValueType.String => "string",
        PropertyValueType.Int => "int",
        PropertyValueType.Double => "double",
        PropertyValueType.Bool => "bool",
        PropertyValueType.Long => "long",
        PropertyValueType.MapInt => "map_int",
        PropertyValueType.MapString => "map_string",
        PropertyValueType.ListString => "list_string",
        _ => "unknown"
    };
}
