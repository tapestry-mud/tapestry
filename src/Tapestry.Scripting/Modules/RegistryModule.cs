using Jint.Native;
using Tapestry.Engine.Persistence;
using Tapestry.Engine.Registration;
using Tapestry.Engine.Tags;
using JintEngine = Jint.Engine;

namespace Tapestry.Scripting.Modules;

public class RegistryModule : IJintApiModule
{
    private readonly RegistrationPolicy _policy;
    private readonly PropertyRegistry _propertyRegistry;
    private readonly TagRegistry _tagRegistry;

    public RegistryModule(RegistrationPolicy policy, PropertyRegistry propertyRegistry, TagRegistry tagRegistry)
    {
        _policy = policy;
        _propertyRegistry = propertyRegistry;
        _tagRegistry = tagRegistry;
    }

    public string Namespace => "registry";

    public object Build(JintEngine engine)
    {
        return new
        {
            summary = new Func<object[]>(GetSummary),
            list = new Func<string, JsValue, object[]>((kind, nameVal) =>
            {
                var name = nameVal == null || nameVal == JsValue.Null || nameVal == JsValue.Undefined
                    ? null
                    : nameVal.ToString();
                return GetList(kind, name);
            }),
            conflicts = new Func<object[]>(GetConflicts)
        };
    }

    private object[] GetSummary()
    {
        var rows = new List<(string Kind, int Count, int Conflicts, string Model)>();
        foreach (var row in _policy.GetRegistrySummary())
        {
            rows.Add((row.Kind, row.Count, row.ConflictCount, row.Model));
        }
        var allProps = _propertyRegistry.GetAll();
        var propAmbig = allProps
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Count(g => g.Count() > 1);
        rows.Add(("property", allProps.Count, propAmbig, "namespaced"));
        var allTags = _tagRegistry.GetAll();
        var tagAmbig = allTags
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Count(g => g.Count() > 1);
        rows.Add(("tag", allTags.Count, tagAmbig, "namespaced"));
        return rows
            .Select(r => (object)new { kind = r.Kind, count = r.Count, conflicts = r.Conflicts, model = r.Model })
            .ToArray();
    }

    private object[] GetList(string kind, string? name)
    {
        if (string.Equals(kind, "property", StringComparison.OrdinalIgnoreCase))
        {
            return GetPropertyList(name);
        }
        if (string.Equals(kind, "tag", StringComparison.OrdinalIgnoreCase))
        {
            return GetTagList(name);
        }
        return _policy.GetRegistrations(kind, name)
            .Select(v => (object)new
            {
                kind = v.Kind,
                name = v.Name,
                owner = v.Owner,
                sourceFile = v.SourceFile,
                line = v.Line,
                isWinner = v.IsWinner,
                isOverride = v.IsOverride,
                shadows = v.Shadows,
                shadowedBy = v.ShadowedBy,
                model = "policy"
            })
            .ToArray();
    }

    private object[] GetPropertyList(string? name)
    {
        var all = _propertyRegistry.GetAll();
        IEnumerable<PropertyRegistryEntry> pool = name == null
            ? all
            : all.Where(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        var results = new List<object>();
        foreach (var group in pool.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            var entries = group.ToList();
            var isAmbiguous = entries.Count > 1;
            var ambiguousOwners = isAmbiguous
                ? entries.Select(e => e.Scope).ToArray()
                : Array.Empty<string>();
            foreach (var e in entries)
            {
                results.Add(new
                {
                    kind = "property",
                    name = e.Name,
                    owner = e.Scope,
                    sourceFile = (string?)null,
                    line = 0,
                    isWinner = true,
                    isOverride = false,
                    shadows = (string?)null,
                    shadowedBy = (string?)null,
                    model = "namespaced",
                    isAmbiguous,
                    ambiguousOwners,
                    valueType = AttributeWriter.ValueTypeName(e.ValueType),
                    min = e.Min,
                    max = e.Max,
                    @enum = e.EnumValues?.ToArray() ?? Array.Empty<string>(),
                    transient = e.Transient,
                    appliesTo = e.AppliesTo?.ToArray() ?? Array.Empty<string>()
                });
            }
        }
        return results.ToArray();
    }

    private object[] GetTagList(string? name)
    {
        var all = _tagRegistry.GetAll();
        IEnumerable<TagRegistryEntry> pool = name == null
            ? all
            : all.Where(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        var results = new List<object>();
        foreach (var group in pool.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
        {
            var entries = group.ToList();
            var isAmbiguous = entries.Count > 1;
            var ambiguousOwners = isAmbiguous
                ? entries.Select(e => e.Scope).ToArray()
                : Array.Empty<string>();
            foreach (var e in entries)
            {
                results.Add(new
                {
                    kind = "tag",
                    name = e.Name,
                    owner = e.Scope,
                    sourceFile = (string?)null,
                    line = 0,
                    isWinner = true,
                    isOverride = false,
                    shadows = (string?)null,
                    shadowedBy = (string?)null,
                    model = "namespaced",
                    isAmbiguous,
                    ambiguousOwners,
                    appliesTo = e.AppliesTo.ToArray(),
                    tagKind = e.Kind
                });
            }
        }
        return results.ToArray();
    }

    private object[] GetConflicts()
    {
        var results = new List<object>();

        foreach (var row in _policy.GetRegistrySummary().Where(r => r.ConflictCount > 0))
        {
            foreach (var v in _policy.GetRegistrations(row.Kind).Where(v => v.Shadows != null || v.ShadowedBy != null))
            {
                results.Add(new
                {
                    kind = v.Kind,
                    name = v.Name,
                    owner = v.Owner,
                    sourceFile = v.SourceFile,
                    line = v.Line,
                    isWinner = v.IsWinner,
                    isOverride = v.IsOverride,
                    shadows = v.Shadows,
                    shadowedBy = v.ShadowedBy,
                    model = "policy"
                });
            }
        }

        foreach (var group in _propertyRegistry.GetAll()
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            var owners = group.Select(e => e.Scope).ToArray();
            foreach (var e in group)
            {
                results.Add(new
                {
                    kind = "property",
                    name = e.Name,
                    owner = e.Scope,
                    sourceFile = (string?)null,
                    line = 0,
                    isWinner = true,
                    isOverride = false,
                    shadows = (string?)null,
                    shadowedBy = (string?)null,
                    model = "namespaced",
                    isAmbiguous = true,
                    ambiguousOwners = owners
                });
            }
        }

        foreach (var group in _tagRegistry.GetAll()
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1))
        {
            var owners = group.Select(e => e.Scope).ToArray();
            foreach (var e in group)
            {
                results.Add(new
                {
                    kind = "tag",
                    name = e.Name,
                    owner = e.Scope,
                    sourceFile = (string?)null,
                    line = 0,
                    isWinner = true,
                    isOverride = false,
                    shadows = (string?)null,
                    shadowedBy = (string?)null,
                    model = "namespaced",
                    isAmbiguous = true,
                    ambiguousOwners = owners
                });
            }
        }

        return results.ToArray();
    }
}
