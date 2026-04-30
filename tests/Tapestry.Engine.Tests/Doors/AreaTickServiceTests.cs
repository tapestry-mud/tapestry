using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests.Doors;

public class AreaTickServiceTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private AreaRegistry _areaRegistry = null!;
    private AreaTickService _areaTick = null!;

    private void Setup(int resetInterval = 1, float occupiedModifier = 1.0f)
    {
        _world = new World();
        _eventBus = new EventBus();
        _areaRegistry = new AreaRegistry();
        _areaRegistry.Register(new AreaDefinition
        {
            Id = "test-area",
            Name = "Test Area",
            ResetInterval = resetInterval,
            OccupiedModifier = occupiedModifier
        });
        _areaTick = new AreaTickService(_world, _eventBus, _areaRegistry, new ServerConfig());
    }

    private Room AddRoom(string id, int players = 0)
    {
        var room = new Room(id, id, "");
        room.Area = "test-area";
        for (var i = 0; i < players; i++)
        {
            var player = new Entity("player", $"player{i}");
            room.AddEntity(player);
        }
        _world.AddRoom(room);
        return room;
    }

    [Fact]
    public void GetPlayerCount_CountsPlayersInArea()
    {
        Setup();
        AddRoom("core:inn", players: 2);
        AddRoom("core:market", players: 1);
        var other = new Room("other:town", "Other Town", "");
        other.Area = "other-area";
        _world.AddRoom(other);

        _areaTick.GetPlayerCount("test-area").Should().Be(3);
        _areaTick.GetPlayerCount("other-area").Should().Be(0);
    }

    [Fact]
    public void GetPlayerCount_ExcludesNpcs()
    {
        Setup();
        var room = AddRoom("core:inn");
        var npc = new Entity("npc", "Guard");
        room.AddEntity(npc);

        _areaTick.GetPlayerCount("test-area").Should().Be(0);
    }

    [Fact]
    public void Tick_FiresAreaTickEvent_WhenIntervalElapsed()
    {
        Setup(resetInterval: 1);
        AddRoom("core:inn");
        GameEvent? captured = null;
        _eventBus.Subscribe("area.tick", e => { captured = e; });

        _areaTick.Tick();

        captured.Should().NotBeNull();
        captured!.Data["areaId"].Should().Be("test-area");
        Convert.ToInt32(captured.Data["tickCount"]).Should().Be(1);
    }

    [Fact]
    public void Tick_IncrementsTickCount()
    {
        Setup(resetInterval: 1);
        AddRoom("core:inn");

        _areaTick.Tick();
        _areaTick.Tick();
        _areaTick.Tick();

        _areaTick.GetAreaState("test-area")!.TickCount.Should().Be(3);
    }

    [Fact]
    public void Tick_RespectsInterval_DoesNotFireEarly()
    {
        Setup(resetInterval: 9999);
        AddRoom("core:inn");
        var count = 0;
        _eventBus.Subscribe("area.tick", _ => { count++; });

        _areaTick.Tick();

        count.Should().Be(0);
    }

    [Fact]
    public void Tick_OccupiedModifier_SlowsResetWhenPlayersPresent()
    {
        Setup(resetInterval: 2, occupiedModifier: 3.0f);
        AddRoom("core:inn", players: 1);
        var count = 0;
        _eventBus.Subscribe("area.tick", _ => { count++; });

        // Effective interval = 2 * 3.0 = 6 ticks
        for (var i = 0; i < 5; i++) { _areaTick.Tick(); }
        count.Should().Be(0);

        _areaTick.Tick(); // 6th tick fires
        count.Should().Be(1);
    }

    [Fact]
    public void SetResetInterval_OverridesAreaDefault()
    {
        Setup(resetInterval: 9999);
        AddRoom("core:inn");
        _areaTick.SetResetInterval("test-area", 1);
        GameEvent? captured = null;
        _eventBus.Subscribe("area.tick", e => { captured = e; });

        _areaTick.Tick();

        captured.Should().NotBeNull();
    }

    [Fact]
    public void SetOccupiedModifier_OverridesAreaDefault()
    {
        Setup(resetInterval: 1, occupiedModifier: 100.0f);
        AddRoom("core:inn", players: 1);
        _areaTick.SetOccupiedModifier("test-area", 1.0f);
        var count = 0;
        _eventBus.Subscribe("area.tick", _ => { count++; });

        _areaTick.Tick();

        count.Should().Be(1);
    }

    [Fact]
    public void GetAreaState_UnknownArea_ReturnsNull()
    {
        Setup();
        _areaTick.GetAreaState("nonexistent").Should().BeNull();
    }
}
