using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests.Doors;

public class TemporaryExitServiceTests
{
    private World _world = null!;
    private EventBus _eventBus = null!;
    private AreaTickService _areaTick = null!;
    private TemporaryExitService _portals = null!;

    private void Setup()
    {
        _world = new World();
        _eventBus = new EventBus();
        _areaTick = new AreaTickService(_world, _eventBus, new AreaRegistry(), new Tapestry.Data.ServerConfig());
        _portals = new TemporaryExitService(_world, _eventBus, _areaTick);
    }

    private Room AddRoom(string id)
    {
        var room = new Room(id, id, "");
        _world.AddRoom(room);
        return room;
    }

    private void FireTick(string prefix, int tickCount)
    {
        _eventBus.Publish(new GameEvent
        {
            Type = "area.tick",
            Data = new Dictionary<string, object?>
            {
                ["areaPrefix"] = prefix,
                ["tickCount"] = tickCount,
                ["playerCount"] = 0
            }
        });
    }

    [Fact]
    public void CreateExit_AddsKeywordExitToRoom()
    {
        Setup();
        AddRoom("core:inn");
        AddRoom("core:cellar");

        var id = _portals.CreateExit("core:inn", "hatch", "core:cellar", 3, "a wooden hatch");

        id.Should().NotBeEmpty();
        _world.GetRoom("core:inn")!.GetKeywordExit("hatch").Should().NotBeNull();
    }

    [Fact]
    public void CreateExit_KeywordAlreadyOccupied_ReturnsEmpty()
    {
        Setup();
        AddRoom("core:inn");
        AddRoom("core:cellar");
        _portals.CreateExit("core:inn", "hatch", "core:cellar", 3);

        var id = _portals.CreateExit("core:inn", "hatch", "core:cellar", 3);

        id.Should().BeEmpty();
    }

    [Fact]
    public void CreateExit_PublishesPortalOpenedEvent()
    {
        Setup();
        AddRoom("core:inn");
        AddRoom("core:cellar");
        GameEvent? captured = null;
        _eventBus.Subscribe("portal.opened", e => { captured = e; });

        _portals.CreateExit("core:inn", "hatch", "core:cellar", 3, "a wooden hatch");

        captured.Should().NotBeNull();
        captured!.Data["keyword"].Should().Be("hatch");
    }

    [Fact]
    public void CreatePairedExit_AddsBothSides()
    {
        Setup();
        var src = AddRoom("core:inn");
        var tgt = AddRoom("core:cellar");

        _portals.CreatePairedExit("core:inn", "hatch", "core:cellar", "ladder", 3, "a gate");

        src.GetKeywordExit("hatch").Should().NotBeNull();
        tgt.GetKeywordExit("ladder").Should().NotBeNull();
    }

    [Fact]
    public void AreaTick_ExpiresExit_RemovesFromRoom()
    {
        Setup();
        AddRoom("core:inn");
        AddRoom("core:cellar");
        _portals.CreateExit("core:inn", "hatch", "core:cellar", 2);

        FireTick("core", 3); // tick 3 >= expiry of currentTick+2

        _world.GetRoom("core:inn")!.HasKeywordExit("hatch").Should().BeFalse();
    }

    [Fact]
    public void AreaTick_ExpiresExit_PublishesPortalClosedEvent()
    {
        Setup();
        AddRoom("core:inn");
        AddRoom("core:cellar");
        _portals.CreateExit("core:inn", "hatch", "core:cellar", 1);
        GameEvent? captured = null;
        _eventBus.Subscribe("portal.closed", e => { captured = e; });

        FireTick("core", 999);

        captured.Should().NotBeNull();
        captured!.Data["keyword"].Should().Be("hatch");
    }

    [Fact]
    public void AreaTick_ExpiresPairedExit_RemovesBothSides()
    {
        Setup();
        AddRoom("core:inn");
        AddRoom("core:cellar");
        _portals.CreatePairedExit("core:inn", "hatch", "core:cellar", "ladder", 1);

        FireTick("core", 999);

        _world.GetRoom("core:inn")!.HasKeywordExit("hatch").Should().BeFalse();
        _world.GetRoom("core:cellar")!.HasKeywordExit("ladder").Should().BeFalse();
    }

    [Fact]
    public void RemoveExit_RemovesFromRoom()
    {
        Setup();
        AddRoom("core:inn");
        AddRoom("core:cellar");
        var id = _portals.CreateExit("core:inn", "hatch", "core:cellar", 10);

        _portals.RemoveExit(id);

        _world.GetRoom("core:inn")!.HasKeywordExit("hatch").Should().BeFalse();
    }
}
