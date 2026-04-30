using FluentAssertions;
using Tapestry.Data;
using Tapestry.Engine;
using Tapestry.Shared;
using Xunit;

namespace Tapestry.Engine.Tests;

public class WeatherServiceTests
{
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly AreaRegistry _areaRegistry;
    private readonly WeatherZoneRegistry _zoneRegistry;
    private readonly SessionManager _sessions;
    private readonly WeatherService _service;

    private static WeatherZoneDefinition MakeZone(string id = "temperate")
    {
        return new WeatherZoneDefinition
        {
            Id = id,
            States = ["clear", "rain", "storm"],
            Transitions = new Dictionary<string, Dictionary<string, int>>
            {
                ["clear"] = new() { ["clear"] = 80, ["rain"] = 20 },
                ["rain"] = new() { ["clear"] = 30, ["rain"] = 60, ["storm"] = 10 },
                ["storm"] = new() { ["rain"] = 70, ["storm"] = 30 }
            },
            TerrainMessages = new Dictionary<string, Dictionary<string, WeatherMessages>>
            {
                ["outdoors"] = new()
                {
                    ["rain"] = new WeatherMessages("Rain begins to fall.", null, "The rain stops."),
                    ["storm"] = new WeatherMessages("A storm rolls in.", null, "The storm passes.")
                }
            },
            TerrainTransitions = new Dictionary<string, Dictionary<string, string>>
            {
                ["outdoors"] = new()
                {
                    ["dawn"] = "The sky brightens in the east.",
                    ["day"] = "The sun climbs overhead.",
                    ["dusk"] = "The light fades.",
                    ["night"] = "Darkness falls."
                }
            }
        };
    }

    public WeatherServiceTests()
    {
        _world = new World();
        _eventBus = new EventBus();
        _areaRegistry = new AreaRegistry();
        _zoneRegistry = new WeatherZoneRegistry();
        _sessions = new SessionManager();

        var zone = MakeZone();
        _zoneRegistry.Register(zone);

        _areaRegistry.Register(new AreaDefinition
        {
            Id = "test-area",
            Name = "Test Area",
            WeatherZone = "temperate"
        });

        var config = new ServerConfig();
        config.Game.WeatherRollIntervalHours = 1;
        _service = new WeatherService(_areaRegistry, _zoneRegistry, _world, _sessions, _eventBus, config);
    }

    private Room AddRoom(string id, string area = "test-area", string? terrain = null)
    {
        var room = new Room(id, id, "");
        room.Area = area;
        if (terrain != null) { room.SetProperty("terrain", terrain); }
        _world.AddRoom(room);
        return room;
    }

    // --- ShouldReceiveWeather ---

    [Fact]
    public void ShouldReceiveWeather_OutdoorRoom_ReturnsTrue()
    {
        var room = AddRoom("r1", terrain: "outdoors");

        _service.ShouldReceiveWeather(room).Should().BeTrue();
    }

    [Fact]
    public void ShouldReceiveWeather_IndoorRoom_ReturnsFalse()
    {
        var room = AddRoom("r2");
        room.SetProperty("terrain", "indoors");

        _service.ShouldReceiveWeather(room).Should().BeFalse();
    }

    [Fact]
    public void ShouldReceiveWeather_WeatherExposedIndoorRoom_ReturnsTrue()
    {
        var room = AddRoom("r3");
        room.SetProperty("terrain", "indoors");
        room.WeatherExposed = true;

        _service.ShouldReceiveWeather(room).Should().BeTrue();
    }

    // --- SetWeather / GetCurrentWeather ---

    [Fact]
    public void SetWeather_OverridesState_NoMessageSent()
    {
        _service.SetWeather("test-area", "storm");

        _service.GetCurrentWeather("test-area").Should().Be("storm");
    }

    [Fact]
    public void GetCurrentWeather_UnknownArea_ReturnsClear()
    {
        _service.GetCurrentWeather("unknown-area").Should().Be("clear");
    }

    // --- OnHourChange / weather transition event ---

    [Fact]
    public void OnHourChange_TransitionsWeather_PublishesEvent()
    {
        // Force the zone to always transition clear -> rain by overriding transitions
        _zoneRegistry.Register(new WeatherZoneDefinition
        {
            Id = "deterministic",
            States = ["clear", "rain"],
            Transitions = new Dictionary<string, Dictionary<string, int>>
            {
                ["clear"] = new() { ["rain"] = 100 }
            },
            TerrainMessages = new(),
            TerrainTransitions = new()
        });
        _areaRegistry.Register(new AreaDefinition
        {
            Id = "det-area",
            Name = "Det Area",
            WeatherZone = "deterministic"
        });

        GameEvent? captured = null;
        _eventBus.Subscribe("weather.change", e => { captured = e; });

        _eventBus.Publish(new GameEvent
        {
            Type = "time.hour.change",
            Data = new Dictionary<string, object?> { ["hour"] = 1, ["period"] = "dawn", ["dayCount"] = 0 }
        });

        captured.Should().NotBeNull();
        captured!.Data["areaId"].Should().Be("det-area");
        captured.Data["state"].Should().Be("rain");
        captured.Data["previousState"].Should().Be("clear");
    }

    [Fact]
    public void OnHourChange_NoTransition_DoesNotPublishEvent()
    {
        _zoneRegistry.Register(new WeatherZoneDefinition
        {
            Id = "static",
            States = ["clear"],
            Transitions = new Dictionary<string, Dictionary<string, int>>
            {
                ["clear"] = new() { ["clear"] = 100 }
            },
            TerrainMessages = new(),
            TerrainTransitions = new()
        });
        _areaRegistry.Register(new AreaDefinition
        {
            Id = "static-area",
            Name = "Static Area",
            WeatherZone = "static"
        });

        var events = new List<GameEvent>();
        _eventBus.Subscribe("weather.change", e => { events.Add(e); });

        _eventBus.Publish(new GameEvent
        {
            Type = "time.hour.change",
            Data = new Dictionary<string, object?> { ["hour"] = 1, ["period"] = "dawn", ["dayCount"] = 0 }
        });

        events.Where(e => e.Data.GetValueOrDefault("areaId") as string == "static-area")
              .Should().BeEmpty();
    }

    // --- Message resolution ---

    [Fact]
    public void MessageResolution_RoomOverridesArea()
    {
        var room = AddRoom("r-override");
        room.WeatherMessages["rain"] = new WeatherMessages("Room rain start.", null, null);

        var area = _areaRegistry.Get("test-area")!;
        var zone = _zoneRegistry.Get("temperate")!;

        var msg = _service.ResolveWeatherMessage(room, area, zone, "rain", "start");

        msg.Should().Be("Room rain start.");
    }

    [Fact]
    public void MessageResolution_AreaOverridesTerrain()
    {
        var areaWithMsg = new AreaDefinition
        {
            Id = "area-with-msg",
            Name = "Area With Msg",
            WeatherZone = "temperate",
            WeatherMessages = new Dictionary<string, WeatherMessages>
            {
                ["rain"] = new WeatherMessages("Area rain start.", null, null)
            }
        };
        _areaRegistry.Register(areaWithMsg);

        var room = AddRoom("r-area-msg", area: "area-with-msg");
        var zone = _zoneRegistry.Get("temperate")!;

        var msg = _service.ResolveWeatherMessage(room, areaWithMsg, zone, "rain", "start");

        msg.Should().Be("Area rain start.");
    }

    [Fact]
    public void MessageResolution_FallsBackToTerrain()
    {
        var room = AddRoom("r-terrain", terrain: "outdoors");
        var area = _areaRegistry.Get("test-area")!;
        var zone = _zoneRegistry.Get("temperate")!;

        var msg = _service.ResolveWeatherMessage(room, area, zone, "rain", "start");

        msg.Should().Be("Rain begins to fall.");
    }

    [Fact]
    public void MessageResolution_NoMatch_ReturnsNull()
    {
        var room = AddRoom("r-none", terrain: "outdoors");
        var area = _areaRegistry.Get("test-area")!;
        var zone = _zoneRegistry.Get("temperate")!;

        var msg = _service.ResolveWeatherMessage(room, area, zone, "clear", "start");

        msg.Should().BeNull();
    }

    // --- ResolveTimeMessage ---

    [Fact]
    public void ResolveTimeMessage_FallsBackToTerrainZone()
    {
        var room = AddRoom("r-time", terrain: "outdoors");
        var area = _areaRegistry.Get("test-area")!;
        var zone = _zoneRegistry.Get("temperate")!;

        var msg = _service.ResolveTimeMessage(room, area, zone, "dawn");

        msg.Should().Be("The sky brightens in the east.");
    }

    [Fact]
    public void ResolveTimeMessage_RoomOverrides()
    {
        var room = AddRoom("r-time-room");
        room.TimeMessages["dawn"] = "A unique dawn.";
        var area = _areaRegistry.Get("test-area")!;
        var zone = _zoneRegistry.Get("temperate")!;

        var msg = _service.ResolveTimeMessage(room, area, zone, "dawn");

        msg.Should().Be("A unique dawn.");
    }
}
