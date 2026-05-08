using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;
using Tapestry.Server.Gmcp.Handlers;

namespace Tapestry.Engine.Tests.Gmcp;

public class WorldHandlerTests
{
    private sealed class ActiveConnectionFake : FakeGmcpConnectionManager
    {
        private readonly List<string> _activeIds;

        public ActiveConnectionFake(IEnumerable<string> activeIds)
        {
            _activeIds = new List<string>(activeIds);
        }

        public new IEnumerable<string> GetActiveConnectionIds() => _activeIds;
    }

    private record Harness(
        WorldHandler Handler,
        ActiveConnectionFake ConnectionManager,
        SessionManager Sessions,
        EventBus EventBus,
        GameClock Clock,
        WeatherService Weather,
        Entity Player,
        string ConnectionId);

    private static Harness Build()
    {
        var connId = "conn-world";
        var cm = new ActiveConnectionFake(new[] { connId });
        var sessions = new SessionManager();
        var world = new World();
        var eb = new EventBus();
        var areas = new AreaRegistry();
        var clock = new GameClock(eb, new ServerConfig());
        var weather = new WeatherService(areas, new WeatherZoneRegistry(), world, sessions, eb, new ServerConfig());

        var handler = new WorldHandler(cm, sessions, world, eb, clock, weather);

        var room = new Room("test:room1", "Test Room", "A plain room.") { Area = "testarea" };
        world.AddRoom(room);

        var entity = new Entity("player", "Hero");
        entity.LocationRoomId = room.Id;
        world.TrackEntity(entity);
        room.AddEntity(entity);

        var conn = new FakeConnection(connId);
        sessions.Add(new PlayerSession(conn, entity));
        handler.Configure();

        return new Harness(handler, cm, sessions, eb, clock, weather, entity, connId);
    }

    [Fact]
    public void SendBurst_SendsWorldTimeAndWorldWeather()
    {
        var h = Build();

        h.Handler.SendBurst(h.ConnectionId, h.Player);

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "World.Time");
        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "World.Weather");
    }

    [Fact]
    public void TimeHourChangeEvent_SendsWorldTimeToConnection()
    {
        var h = Build();

        h.EventBus.Publish(new GameEvent
        {
            Type = "time.hour.change",
            Data = new Dictionary<string, object?> { ["hour"] = 8, ["period"] = "morning", ["dayCount"] = 1 }
        });

        h.ConnectionManager.Sent.Should().Contain(x => x.Package == "World.Time");
    }

    [Fact]
    public void PackageNames_ContainsBothPackages()
    {
        var h = Build();
        h.Handler.PackageNames.Should().Contain("World.Time").And.Contain("World.Weather");
    }
}
