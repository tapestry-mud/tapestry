using System.Linq;
using Tapestry.Data;
using Tapestry.Engine.Tags;
using Tapestry.Shared;

namespace Tapestry.Engine;

public class WeatherService
{
    private readonly AreaRegistry _areaRegistry;
    private readonly WeatherZoneRegistry _zoneRegistry;
    private readonly World _world;
    private readonly SessionManager _sessions;
    private readonly EventBus _eventBus;
    private readonly ServerConfig _config;
    private readonly TagRegistry _tagRegistry;
    private readonly Dictionary<string, string> _currentWeather =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();

    // Lazily built the first time a biome lookup is needed. Tag registration happens at
    // pack-load time, before this service resolves any message, so caching after boot
    // is safe -- the registry's biome-kind entries are stable for the process lifetime.
    private HashSet<string>? _biomeTagNames;

    public WeatherService(AreaRegistry areaRegistry, WeatherZoneRegistry zoneRegistry,
        World world, SessionManager sessions, EventBus eventBus, ServerConfig config,
        TagRegistry tagRegistry)
    {
        _areaRegistry = areaRegistry;
        _zoneRegistry = zoneRegistry;
        _world = world;
        _sessions = sessions;
        _eventBus = eventBus;
        _config = config;
        _tagRegistry = tagRegistry;

        _eventBus.Subscribe("time.hour.change", OnHourChange);
        _eventBus.Subscribe("time.period.change", OnPeriodChange);
    }

    public string GetCurrentWeather(string areaId)
    {
        return _currentWeather.GetValueOrDefault(areaId, "clear");
    }

    public void SetWeather(string areaId, string state)
    {
        _currentWeather[areaId] = state;
    }

    // Group the few occupied rooms (those holding a logged-in player) by area id.
    // Weather/time messages only matter to rooms someone is standing in, so this is the
    // work set -- it avoids the old O(areas x all-rooms) scan that grew with world content.
    private Dictionary<string, List<Room>> OccupiedRoomsByArea()
    {
        var byArea = new Dictionary<string, List<Room>>(StringComparer.OrdinalIgnoreCase);
        foreach (var roomId in _sessions.OccupiedRoomIds())
        {
            var room = _world.GetRoom(roomId);
            if (room?.Area == null) { continue; }
            if (!byArea.TryGetValue(room.Area, out var list))
            {
                list = new List<Room>();
                byArea[room.Area] = list;
            }
            list.Add(room);
        }
        return byArea;
    }

    private void OnHourChange(GameEvent evt)
    {
        var hour = Convert.ToInt32(evt.Data["hour"]);
        if (hour % _config.Game.WeatherRollIntervalHours != 0) { return; }

        // Weather state still rolls for every zoned area (cheap, keeps the world consistent),
        // but messages only go to occupied rooms.
        var occupiedByArea = OccupiedRoomsByArea();

        foreach (var area in _areaRegistry.All())
        {
            if (area.WeatherZone == null) { continue; }
            var zone = _zoneRegistry.Get(area.WeatherZone);
            if (zone == null) { continue; }

            var currentState = _currentWeather.GetValueOrDefault(area.Id, "clear");
            var nextState = RollTransition(zone, currentState);

            if (nextState == currentState) { continue; }

            var previousState = currentState;
            _currentWeather[area.Id] = nextState;

            _eventBus.Publish(new GameEvent
            {
                Type = "weather.change",
                Data = new Dictionary<string, object?>
                {
                    ["areaId"] = area.Id,
                    ["state"] = nextState,
                    ["previousState"] = previousState
                }
            });

            if (occupiedByArea.TryGetValue(area.Id, out var rooms))
            {
                SendWeatherMessages(area, zone, previousState, nextState, rooms);
            }
        }
    }

    private void OnPeriodChange(GameEvent evt)
    {
        var period = evt.Data.GetValueOrDefault("period") as string;
        if (period == null) { return; }

        foreach (var (areaId, rooms) in OccupiedRoomsByArea())
        {
            var area = _areaRegistry.Get(areaId);
            if (area == null) { continue; }
            var zone = area.WeatherZone != null ? _zoneRegistry.Get(area.WeatherZone) : null;

            foreach (var room in rooms)
            {
                if (!ShouldReceiveTimeTransition(room)) { continue; }
                var msg = ResolveTimeMessage(room, area, zone, period);
                if (msg == null) { continue; }
                _sessions.SendToRoom(room.Id, msg + "\r\n");
            }
        }
    }

    private string RollTransition(WeatherZoneDefinition zone, string currentState)
    {
        if (!zone.Transitions.TryGetValue(currentState, out var weights)) { return currentState; }
        var total = weights.Values.Sum();
        if (total <= 0) { return currentState; }
        var roll = _random.Next(total);
        var cumulative = 0;
        foreach (var (state, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative) { return state; }
        }
        return currentState;
    }

    private void SendWeatherMessages(AreaDefinition area, WeatherZoneDefinition zone,
        string previousState, string nextState, IReadOnlyList<Room> rooms)
    {
        foreach (var room in rooms)
        {
            if (!ShouldReceiveWeather(room)) { continue; }

            var endMsg = ResolveWeatherMessage(room, area, zone, previousState, "end");
            if (endMsg != null) { _sessions.SendToRoom(room.Id, endMsg + "\r\n"); }

            var startMsg = ResolveWeatherMessage(room, area, zone, nextState, "start");
            if (startMsg != null) { _sessions.SendToRoom(room.Id, startMsg + "\r\n"); }
        }
    }

    private string? GetTerrain(Room room)
    {
        return room.GetProperty<string?>("terrain") ?? "outdoors";
    }

    public bool ShouldReceiveWeather(Room room)
    {
        var terrain = GetTerrain(room);
        var isShielded = terrain == "indoors" || terrain == "underground";
        return !isShielded || room.WeatherExposed;
    }

    public bool ShouldReceiveTimeTransition(Room room)
    {
        var terrain = GetTerrain(room);
        var isShielded = terrain == "indoors" || terrain == "underground";
        return !isShielded || room.TimeExposed;
    }

    public string? ResolveWeatherMessage(Room room, AreaDefinition area,
        WeatherZoneDefinition zone, string state, string messageType)
    {
        if (room.WeatherMessages.TryGetValue(state, out var roomMsg))
        {
            var text = GetMessageField(roomMsg, messageType);
            if (text != null) { return text; }
        }

        if (area.WeatherMessages.TryGetValue(state, out var areaMsg))
        {
            var text = GetMessageField(areaMsg, messageType);
            if (text != null) { return text; }
        }

        var biome = ResolveBiome(room);
        if (biome != null && zone.TerrainMessages.TryGetValue(biome, out var biomeStates) &&
            biomeStates.TryGetValue(state, out var biomeMsg))
        {
            return GetMessageField(biomeMsg, messageType);
        }

        var terrain = GetTerrain(room) ?? "outdoors";
        if (zone.TerrainMessages.TryGetValue(terrain, out var terrainStates) &&
            terrainStates.TryGetValue(state, out var terrainMsg))
        {
            return GetMessageField(terrainMsg, messageType);
        }

        return null;
    }

    public string? ResolveTimeMessage(Room room, AreaDefinition area,
        WeatherZoneDefinition? zone, string period)
    {
        if (room.TimeMessages.TryGetValue(period, out var roomMsg)) { return roomMsg; }
        if (area.TimeMessages.TryGetValue(period, out var areaMsg)) { return areaMsg; }

        var biome = ResolveBiome(room);
        if (biome != null && zone?.TerrainTransitions.TryGetValue(biome, out var biomePeriods) == true &&
            biomePeriods.TryGetValue(period, out var biomeMsg))
        {
            return biomeMsg;
        }

        var terrain = GetTerrain(room) ?? "outdoors";
        if (zone?.TerrainTransitions.TryGetValue(terrain, out var terrainPeriods) == true &&
            terrainPeriods.TryGetValue(period, out var terrainMsg))
        {
            return terrainMsg;
        }

        return null;
    }

    // The biome is a room tag whose registered kind is "biome" (desugared from the room's
    // `biome:` YAML field at load time -- see YamlContentLoader.AddTag). Room storage does
    // not distinguish it from any other tag, so recovering it means intersecting the room's
    // tag set against every biome-kind entry in the TagRegistry. Same pattern as
    // RoomProjector.Project and AreaMapProjector.CollectBiomeTagNames.
    private string? ResolveBiome(Room room)
    {
        _biomeTagNames ??= CollectBiomeTagNames();
        return room.Tags.FirstOrDefault(t => _biomeTagNames.Contains(t));
    }

    private HashSet<string> CollectBiomeTagNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _tagRegistry.GetAll())
        {
            if (string.Equals(entry.Kind, "biome", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(entry.Name);
                names.Add(entry.FullName);
            }
        }
        return names;
    }

    private static string? GetMessageField(WeatherMessages msg, string field)
    {
        return field switch
        {
            "start" => msg.Start,
            "ongoing" => msg.Ongoing,
            "end" => msg.End,
            _ => null
        };
    }
}
