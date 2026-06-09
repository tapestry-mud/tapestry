using Microsoft.Extensions.Logging;
using Tapestry.Shared.Help;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Tapestry.Engine.Help;

public class HelpQueryResult
{
    public string Status { get; set; } = "";
    public string? Term { get; set; }
    public HelpTopic? Topic { get; set; }
    public List<HelpTopicSummary>? Matches { get; set; }
}

public class HelpService
{
    private readonly ILogger<HelpService>? _logger;

    private readonly Dictionary<string, (HelpTopic Topic, int LoadOrder)> _byId
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (HelpTopic Topic, int LoadOrder)> _byTitle
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<HelpTopic>> _byCategory
        = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] RoleHierarchy = ["player", "builder", "admin"];

    // Resolves an entity's roles by id (e.g. World.GetEntity(id).Roles). Null in
    // standalone/test construction, in which case any logged-in entity is treated
    // as plain player tier.
    private readonly Func<string, IEnumerable<string>>? _rolesResolver;

    public HelpService(
        ILogger<HelpService>? logger = null,
        Func<string, IEnumerable<string>>? rolesResolver = null)
    {
        _logger = logger;
        _rolesResolver = rolesResolver;
    }

    public void LoadPack(string packName, string packRoot, string helpGlob, int loadOrder)
    {
        if (string.IsNullOrWhiteSpace(helpGlob)) { return; }

        var helpDir = Path.Combine(packRoot, "help");
        if (!Directory.Exists(helpDir)) { return; }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        // helpGlob serves as an enabled-guard; GetFiles doesn't support ** patterns so all .yaml files are loaded
        foreach (var file in Directory.GetFiles(helpDir, "*.yaml", SearchOption.AllDirectories).OrderBy(f => f))
        {
            try
            {
                var topic = deserializer.Deserialize<HelpTopic>(File.ReadAllText(file));
                if (string.IsNullOrWhiteSpace(topic.Id) || string.IsNullOrWhiteSpace(topic.Title))
                {
                    _logger?.LogWarning("Help topic in {File} missing required fields id or title - skipping", file);
                    continue;
                }
                topic.PackName = packName;
                AddTopic(topic, loadOrder);
                _logger?.LogDebug("  Help topic: {Id}", topic.NamespacedId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load help topic from {File}", file);
            }
        }
    }

    public void AddTopic(HelpTopic topic, int loadOrder = 0)
    {
        Upsert(_byId, topic.Id, topic, loadOrder);
        Upsert(_byId, topic.NamespacedId, topic, loadOrder);
        Upsert(_byTitle, topic.Title, topic, loadOrder);

        if (!_byCategory.ContainsKey(topic.Category)) { _byCategory[topic.Category] = new(); }

        _byCategory[topic.Category].RemoveAll(t => t.Id == topic.Id && t.PackName == topic.PackName);
        _byCategory[topic.Category].Add(topic);
    }

    public HelpQueryResult Query(string? entityId, string term)
    {
        var tier = PlayerTier(entityId);

        if (_byId.TryGetValue(term, out var idHit) && IsVisible(idHit.Topic, tier))
        {
            return new() { Status = "ok", Topic = idHit.Topic };
        }

        if (_byTitle.TryGetValue(term, out var titleHit) && IsVisible(titleHit.Topic, tier))
        {
            return new() { Status = "ok", Topic = titleHit.Topic };
        }

        var fuzzy = _byId.Values
            .Select(x => x.Topic)
            .Where(t => IsVisible(t, tier) && MatchesFuzzy(t, term))
            .DistinctBy(t => t.NamespacedId)
            .ToList();

        if (fuzzy.Count == 1) { return new() { Status = "ok", Topic = fuzzy[0] }; }

        if (fuzzy.Count > 1)
        {
            return new()
            {
                Status = "multiple",
                Term = term,
                Matches = fuzzy.Select(t => new HelpTopicSummary { Id = t.Id, Title = t.Title, Brief = t.Brief }).ToList()
            };
        }

        return new() { Status = "no_match", Term = term };
    }

    public List<HelpTopicSummary> List(string? entityId, string category)
    {
        var tier = PlayerTier(entityId);
        if (!_byCategory.TryGetValue(category, out var topics)) { return new(); }

        return topics
            .Where(t => IsVisible(t, tier))
            .Select(t => new HelpTopicSummary { Id = t.Id, Title = t.Title, Brief = t.Brief })
            .ToList();
    }

    public List<string> Categories(string? entityId)
    {
        var tier = PlayerTier(entityId);
        return _byCategory
            .Where(kv => kv.Value.Any(t => IsVisible(t, tier)))
            .Select(kv => kv.Key)
            .OrderBy(c => c)
            .ToList();
    }

    // load_order no longer gates help resolution. The RegistrationPolicy (Kind "help")
    // decides cross-pack winners; this is an unconditional set so the resolved winner wins.
    // The int slot is retained only to keep the dict shape and AddTopic signature stable.
    private static void Upsert(
        Dictionary<string, (HelpTopic, int)> dict,
        string key,
        HelpTopic topic,
        int loadOrder)
    {
        dict[key] = (topic, loadOrder);
    }

    private static bool MatchesFuzzy(HelpTopic t, string term) =>
        t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
        || t.Keywords.Any(k => k.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static int RoleTier(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) { return -1; }
        var idx = Array.IndexOf(RoleHierarchy, role.ToLowerInvariant());
        return idx;
    }

    private static bool IsVisible(HelpTopic t, int playerTier) =>
        RoleTier(t.Role) <= playerTier;

    // -1 = no player (chargen) -- only role-less visible.
    // A logged-in entity is at least player tier; builder/admin roles elevate it
    // so role-gated help (link, spawn, etc.) becomes visible to those who hold them.
    private int PlayerTier(string? entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId)) { return -1; }

        var tier = RoleTier("player");
        if (_rolesResolver != null)
        {
            foreach (var role in _rolesResolver(entityId))
            {
                tier = Math.Max(tier, RoleTier(role));
            }
        }
        return tier;
    }
}
