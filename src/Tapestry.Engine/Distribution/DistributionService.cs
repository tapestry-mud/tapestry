using Microsoft.Extensions.Logging;
using Tapestry.Engine.Items;
using Tapestry.Shared;

namespace Tapestry.Engine.Distribution;

/// <summary>
/// Places item instances into mobs (at mob.spawn) and rooms (replenish-to-max at area.tick
/// and initial boot). Reads spawn_on entries from ItemTemplate; caches templates at Initialize.
/// </summary>
public class DistributionService
{
    private readonly World _world;
    private readonly ItemRegistry _itemRegistry;
    private readonly Random _random;
    private readonly ILogger<DistributionService>? _logger;
    private List<ItemTemplate> _distributionTemplates = new();

    /// <summary>Property key stamped on distributed item instances; used for replenish top-up counting.</summary>
    private const string DistributedFromKey = "distributed_from";

    public DistributionService(
        EventBus eventBus,
        World world,
        ItemRegistry itemRegistry,
        Random? random = null,
        ILogger<DistributionService>? logger = null)
    {
        _world = world;
        _itemRegistry = itemRegistry;
        _random = random ?? Random.Shared;
        _logger = logger;

        eventBus.Subscribe("mob.spawn", OnMobSpawn);
        eventBus.Subscribe("area.tick", OnAreaTick);
    }

    public void Initialize(IEnumerable<ItemTemplate> allTemplates)
    {
        _distributionTemplates = allTemplates.Where(t => t.SpawnOn.Count > 0).ToList();
        _logger?.LogInformation(
            "DistributionService: {Count} item template(s) with spawn_on entries cached",
            _distributionTemplates.Count);
    }

    public void SeedAllRooms()
    {
        foreach (var room in _world.AllRooms)
        {
            SeedRoom(room);
        }
        SeedGlobalEntries();
    }

    private void OnMobSpawn(GameEvent evt)
    {
        if (evt.SourceEntityId == null) { return; }
        var entity = _world.GetEntity(evt.SourceEntityId.Value);
        if (entity == null) { return; }

        foreach (var template in _distributionTemplates)
        {
            foreach (var entry in template.SpawnOn)
            {
                if (entry.Selector.Shop) { continue; }
                if (!EntitySelector.MatchesEntity(entity, entry.Selector)) { continue; }

                for (var i = 0; i < entry.Count; i++)
                {
                    if (_random.NextDouble() > entry.Chance) { continue; }
                    var item = _itemRegistry.CreateItem(template.Id);
                    if (item == null) { continue; }
                    item.SetProperty(DistributedFromKey, template.Id);
                    entity.AddToContents(item);
                    _world.TrackEntity(item);
                }
            }
        }
    }

    private void OnAreaTick(GameEvent evt)
    {
        var areaId = evt.Data.GetValueOrDefault("areaId") as string;
        if (areaId == null) { return; }
        var rooms = _world.AllRooms.Where(r => r.Area == areaId);
        foreach (var room in rooms)
        {
            SeedRoom(room);
        }
        SeedGlobalEntries();
    }

    private void SeedGlobalEntries()
    {
        foreach (var template in _distributionTemplates)
        {
            foreach (var entry in template.SpawnOn)
            {
                if (entry.Scope != SpawnScope.Global) { continue; }
                if (entry.Selector.Shop) { continue; }

                var matchingRooms = _world.AllRooms
                    .Where(r => EntitySelector.MatchesRoom(r, entry.Selector))
                    .ToList();

                if (matchingRooms.Count == 0) { continue; }

                var existingTotal = matchingRooms.Sum(r => r.Entities.Count(e => string.Equals(
                    e.GetProperty<string>(DistributedFromKey), template.Id, StringComparison.OrdinalIgnoreCase)));

                var needed = entry.Count - existingTotal;
                for (var i = 0; i < needed; i++)
                {
                    if (_random.NextDouble() > entry.Chance) { continue; }
                    var room = matchingRooms[_random.Next(matchingRooms.Count)];
                    var item = _itemRegistry.CreateItem(template.Id);
                    if (item == null) { continue; }
                    item.SetProperty(DistributedFromKey, template.Id);
                    room.AddEntity(item);
                    _world.TrackEntity(item);
                }
            }
        }
    }

    private void SeedRoom(Room room)
    {
        foreach (var template in _distributionTemplates)
        {
            foreach (var entry in template.SpawnOn)
            {
                if (entry.Scope == SpawnScope.Global) { continue; }
                if (entry.Selector.Shop) { continue; }
                if (!EntitySelector.MatchesRoom(room, entry.Selector)) { continue; }

                var existing = room.Entities.Count(e => string.Equals(
                    e.GetProperty<string>(DistributedFromKey), template.Id, StringComparison.OrdinalIgnoreCase));

                var needed = entry.Count - existing;
                for (var i = 0; i < needed; i++)
                {
                    if (_random.NextDouble() > entry.Chance) { continue; }
                    var item = _itemRegistry.CreateItem(template.Id);
                    if (item == null) { continue; }
                    item.SetProperty(DistributedFromKey, template.Id);
                    room.AddEntity(item);
                    _world.TrackEntity(item);
                }
            }
        }
    }
}
