using Tapestry.Engine.Distribution;
using Tapestry.Engine.Items;
using Tapestry.Engine.Mobs;
using Tapestry.Shared;

namespace Tapestry.Engine.Tests.Distribution;

public class DistributionServiceTests
{
    private World _world = null!;
    private EventBus _bus = null!;
    private ItemRegistry _items = null!;
    private SpawnManager _spawner = null!;
    private DistributionService _svc = null!;

    private void Setup()
    {
        _world = new World();
        _bus = new EventBus();
        _items = new ItemRegistry();
        _spawner = new SpawnManager(_world, _bus, new LootTableResolver(), _items);
        _svc = new DistributionService(_bus, _world, _items, new Random(42));
    }

    [Fact]
    public void MobSpawn_TemplateMatchesTag_PlacesItemInMobContents()
    {
        Setup();
        var copper = new ItemTemplate
        {
            Id = "test:copper-chunk", Name = "copper chunk", Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "cave-dweller"), Chance: 1.0, Count: 1) }
        };
        _items.Register(copper);
        _svc.Initialize(_items.AllTemplates);

        var room = new Room("r1", "Cave", ".");
        _world.AddRoom(room);

        var mobTemplate = new MobTemplate { Id = "test:bat", Name = "Bat", Type = "npc" };
        mobTemplate.Tags.Add("cave-dweller");
        _spawner.RegisterTemplate(mobTemplate);

        var mob = _spawner.SpawnMob("test:bat", "r1");
        Assert.NotNull(mob);

        var contents = mob!.Contents.ToList();
        Assert.Single(contents);
        Assert.Equal("test:copper-chunk", contents[0].GetProperty<string>(CommonProperties.TemplateId));
    }

    [Fact]
    public void MobSpawn_ChanceZero_NeverPlaces()
    {
        Setup();
        var copper = new ItemTemplate
        {
            Id = "test:copper-chunk", Name = "copper chunk", Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "cave-dweller"), Chance: 0.0, Count: 1) }
        };
        _items.Register(copper);
        _svc.Initialize(_items.AllTemplates);
        var room = new Room("r1", "Cave", "."); _world.AddRoom(room);
        var mobTemplate = new MobTemplate { Id = "test:bat", Name = "Bat", Type = "npc" };
        mobTemplate.Tags.Add("cave-dweller");
        _spawner.RegisterTemplate(mobTemplate);
        var mob = _spawner.SpawnMob("test:bat", "r1");
        Assert.NotNull(mob);
        Assert.Empty(mob!.Contents);
    }

    [Fact]
    public void MobSpawn_ShopEntry_NotPlacedInMob()
    {
        Setup();
        var copper = new ItemTemplate
        {
            Id = "test:copper-chunk", Name = "copper chunk", Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Shop: true), Chance: 1.0, Count: 1) }
        };
        _items.Register(copper);
        _svc.Initialize(_items.AllTemplates);
        var room = new Room("r1", "Cave", "."); _world.AddRoom(room);
        var mobTemplate = new MobTemplate { Id = "test:bat", Name = "Bat", Type = "npc" };
        _spawner.RegisterTemplate(mobTemplate);
        var mob = _spawner.SpawnMob("test:bat", "r1");
        Assert.NotNull(mob);
        Assert.Empty(mob!.Contents);
    }

    [Fact]
    public void SeedAllRooms_TaggedRoom_PlacesUpToCount()
    {
        Setup();
        var wood = new ItemTemplate
        {
            Id = "test:wood-chunk", Name = "wood chunk", Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "forest-room"), Chance: 1.0, Count: 3) }
        };
        _items.Register(wood);
        _svc.Initialize(_items.AllTemplates);
        var room = new Room("r1", "Forest", "."); room.Area = "test-area"; room.AddTag("forest-room");
        _world.AddRoom(room);
        _svc.SeedAllRooms();
        var woodItems = room.Entities.Where(e => e.GetProperty<string>(CommonProperties.TemplateId) == "test:wood-chunk").ToList();
        Assert.Equal(3, woodItems.Count);
    }

    [Fact]
    public void SeedAllRooms_Idempotent_DoesNotExceedCount()
    {
        Setup();
        var wood = new ItemTemplate
        {
            Id = "test:wood-chunk", Name = "wood chunk", Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "forest-room"), Chance: 1.0, Count: 3) }
        };
        _items.Register(wood);
        _svc.Initialize(_items.AllTemplates);
        var room = new Room("r1", "Forest", "."); room.Area = "test-area"; room.AddTag("forest-room");
        _world.AddRoom(room);
        _svc.SeedAllRooms();
        _svc.SeedAllRooms();
        var woodItems = room.Entities.Where(e => e.GetProperty<string>(CommonProperties.TemplateId) == "test:wood-chunk").ToList();
        Assert.Equal(3, woodItems.Count);
    }

    [Fact]
    public void SeedAllRooms_RoomAlreadyHasSome_TopsUp()
    {
        Setup();
        var wood = new ItemTemplate
        {
            Id = "test:wood-chunk", Name = "wood chunk", Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "forest-room"), Chance: 1.0, Count: 3) }
        };
        _items.Register(wood);
        _svc.Initialize(_items.AllTemplates);
        var room = new Room("r1", "Forest", "."); room.Area = "test-area"; room.AddTag("forest-room");
        _world.AddRoom(room);

        var existing = _items.CreateItem("test:wood-chunk")!;
        existing.SetProperty("distributed_from", "test:wood-chunk");
        room.AddEntity(existing);
        _world.TrackEntity(existing);

        _svc.SeedAllRooms();
        var woodItems = room.Entities.Where(e => e.GetProperty<string>(CommonProperties.TemplateId) == "test:wood-chunk").ToList();
        Assert.Equal(3, woodItems.Count);
    }

    [Fact]
    public void SeedAllRooms_UntaggedRoom_PlacesNothing()
    {
        Setup();
        var wood = new ItemTemplate
        {
            Id = "test:wood-chunk", Name = "wood chunk", Type = "item",
            SpawnOn = new List<SpawnOnEntry> { new(new SelectorSpec(Tag: "forest-room"), Chance: 1.0, Count: 3) }
        };
        _items.Register(wood);
        _svc.Initialize(_items.AllTemplates);
        var room = new Room("r1", "Dungeon", "."); room.Area = "test-area";
        _world.AddRoom(room);
        _svc.SeedAllRooms();
        var woodItems = room.Entities.Where(e => e.GetProperty<string>(CommonProperties.TemplateId) == "test:wood-chunk").ToList();
        Assert.Empty(woodItems);
    }
}
