using Tapestry.Contracts;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Server.Gmcp;

namespace Tapestry.Server.Gmcp.Handlers;

public class WorldHandler : IGmcpPackageHandler
{
    private readonly IGmcpConnectionManager _connectionManager;
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly GameClock _gameClock;
    private readonly WeatherService _weatherService;

    public string Name => "World";
    public IReadOnlyList<string> PackageNames { get; } = new[] { "World.Time", "World.Weather" };

    public WorldHandler(
        IGmcpConnectionManager connectionManager,
        SessionManager sessions,
        World world,
        EventBus eventBus,
        GameClock gameClock,
        WeatherService weatherService)
    {
        _connectionManager = connectionManager;
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _gameClock = gameClock;
        _weatherService = weatherService;
    }

    public void Configure()
    {
        _eventBus.Subscribe("time.hour.change", evt =>
        {
            var hour = Convert.ToInt32(evt.Data["hour"]);
            var period = evt.Data.GetValueOrDefault("period") as string ?? "";
            var dayCount = Convert.ToInt32(evt.Data.GetValueOrDefault("dayCount") ?? 0);

            foreach (var connectionId in _connectionManager.GetActiveConnectionIds())
            {
                _connectionManager.Send(connectionId, "World.Time", new { hour, period, dayCount });
            }
        });

        _eventBus.Subscribe("weather.change", evt =>
        {
            var areaId = evt.Data.GetValueOrDefault("areaId") as string;
            var state = evt.Data.GetValueOrDefault("state") as string ?? "clear";
            if (areaId == null) { return; }

            foreach (var session in _sessions.AllSessions)
            {
                if (session.PlayerEntity?.LocationRoomId == null) { continue; }
                var room = _world.GetRoom(session.PlayerEntity.LocationRoomId);
                if (room == null) { continue; }
                if (!string.Equals(room.Area, areaId, StringComparison.OrdinalIgnoreCase)) { continue; }
                _connectionManager.Send(session.Connection.Id, "World.Weather", new { state });
            }
        });
    }

    public void SendBurst(string connectionId, object entity)
    {
        var e = (Entity)entity;
        _connectionManager.Send(connectionId, "World.Time", new
        {
            hour = _gameClock.CurrentHour,
            period = _gameClock.CurrentPeriod.ToString().ToLower(),
            dayCount = _gameClock.DayCount,
        });

        if (e.LocationRoomId != null)
        {
            var room = _world.GetRoom(e.LocationRoomId);
            if (room?.Area != null)
            {
                var state = _weatherService.GetCurrentWeather(room.Area);
                _connectionManager.Send(connectionId, "World.Weather", new { state });
            }
        }
    }
}
