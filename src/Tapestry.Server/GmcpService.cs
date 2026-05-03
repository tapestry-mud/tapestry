using System.Collections.Concurrent;
using Tapestry.Engine;
using Tapestry.Engine.Abilities;
using Tapestry.Engine.Alignment;
using Tapestry.Engine.Color;
using Tapestry.Engine.Combat;
using Tapestry.Engine.Economy;
using Tapestry.Engine.Effects;
using Tapestry.Engine.Inventory;
using Tapestry.Engine.Items;
using Tapestry.Engine.Progression;
using Tapestry.Engine.Sustenance;
using Tapestry.Shared;

namespace Tapestry.Server;

public class GmcpService
{
    private readonly SessionManager _sessions;
    private readonly World _world;
    private readonly EventBus _eventBus;
    private readonly GameClock _gameClock;
    private readonly WeatherService _weatherService;
    private readonly ProgressionManager _progressionManager;
    private readonly AlignmentManager _alignmentManager;
    private readonly SustenanceConfig _sustenanceConfig;
    private readonly CommandRegistry _commandRegistry;
    private readonly EffectManager _effectManager;
    private readonly CombatManager _combatManager;
    private readonly AbilityRegistry _abilityRegistry;
    private readonly ThemeRegistry _themeRegistry;
    private readonly RarityRegistry _rarityRegistry;
    private readonly EssenceRegistry _essenceRegistry;
    private readonly SlotRegistry _slotRegistry;
    private readonly ConcurrentDictionary<string, IGmcpHandler> _handlers = new();
    private readonly HashSet<Guid> _dirtyVitals = new();
    private readonly object _dirtyLock = new();

    public GmcpService(SessionManager sessions, World world, EventBus eventBus,
        GameClock gameClock, WeatherService weatherService,
        ProgressionManager progressionManager, AlignmentManager alignmentManager,
        SustenanceConfig sustenanceConfig, CommandRegistry commandRegistry,
        EffectManager effectManager, CombatManager combatManager,
        AbilityRegistry abilityRegistry, ThemeRegistry themeRegistry,
        RarityRegistry rarityRegistry, EssenceRegistry essenceRegistry,
        SlotRegistry slotRegistry)
    {
        _sessions = sessions;
        _world = world;
        _eventBus = eventBus;
        _gameClock = gameClock;
        _weatherService = weatherService;
        _progressionManager = progressionManager;
        _alignmentManager = alignmentManager;
        _sustenanceConfig = sustenanceConfig;
        _commandRegistry = commandRegistry;
        _effectManager = effectManager;
        _combatManager = combatManager;
        _abilityRegistry = abilityRegistry;
        _themeRegistry = themeRegistry;
        _rarityRegistry = rarityRegistry;
        _essenceRegistry = essenceRegistry;
        _slotRegistry = slotRegistry;

        _eventBus.Subscribe("player.moved", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            var entity = _world.GetEntity(evt.SourceEntityId.Value);
            if (entity == null) { return; }
            var session = _sessions.GetByEntityId(evt.SourceEntityId.Value);
            if (session == null) { return; }
            if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }
            SendRoomNearby(handler, entity);
        });

        _eventBus.Subscribe("progression.xp.gained", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCharExperience(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("progression.level.up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCharExperience(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("time.hour.change", evt =>
        {
            var hour = Convert.ToInt32(evt.Data["hour"]);
            var period = evt.Data.GetValueOrDefault("period") as string ?? "";
            var dayCount = Convert.ToInt32(evt.Data.GetValueOrDefault("dayCount") ?? 0);
            foreach (var session in _sessions.AllSessions)
            {
                if (!_handlers.TryGetValue(session.Connection.Id, out var h)) { continue; }
                h.Send("World.Time", new { hour, period, dayCount });
            }
        });

        _eventBus.Subscribe("weather.change", evt =>
        {
            var areaId = evt.Data.GetValueOrDefault("areaId") as string;
            var state  = evt.Data.GetValueOrDefault("state")  as string ?? "clear";
            if (areaId == null) { return; }

            foreach (var session in _sessions.AllSessions)
            {
                if (session.PlayerEntity?.LocationRoomId == null) { continue; }
                var room = _world.GetRoom(session.PlayerEntity.LocationRoomId);
                if (room == null) { continue; }
                if (!string.Equals(room.Area, areaId, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (!_handlers.TryGetValue(session.Connection.Id, out var h)) { continue; }
                h.Send("World.Weather", new { state });
            }
        });

        _eventBus.Subscribe("effect.applied", evt =>
        {
            if (!evt.TargetEntityId.HasValue) { return; }
            SendCharEffects(evt.TargetEntityId.Value);
        });

        _eventBus.Subscribe("effect.removed", evt =>
        {
            if (!evt.TargetEntityId.HasValue) { return; }
            SendCharEffects(evt.TargetEntityId.Value);
        });

        _eventBus.Subscribe("effect.expired", evt =>
        {
            if (!evt.TargetEntityId.HasValue) { return; }
            SendCharEffects(evt.TargetEntityId.Value);
        });

        _eventBus.Subscribe("combat.engage", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCharCombatTarget(evt.SourceEntityId.Value);
            SendCharCombatTargets(evt.SourceEntityId.Value);
            if (evt.TargetEntityId.HasValue)
            {
                SendCharCombatTarget(evt.TargetEntityId.Value);
                SendCharCombatTargets(evt.TargetEntityId.Value);
            }
        });

        _eventBus.Subscribe("combat.hit", evt =>
        {
            // Push Room.Nearby updates (health tier) to room occupants
            if (evt.RoomId != null)
            {
                var room = _world.GetRoom(evt.RoomId);
                if (room != null)
                {
                    foreach (var occupant in room.Entities.Where(e => e.Type == "player"))
                    {
                        var occupantSession = _sessions.GetByEntityId(occupant.Id);
                        if (occupantSession == null) { continue; }
                        if (!_handlers.TryGetValue(occupantSession.Connection.Id, out var h)) { continue; }
                        SendRoomNearby(h, occupant);
                    }
                }
            }

            // Update combat target health for the attacker
            if (evt.SourceEntityId.HasValue)
            {
                SendCharCombatTarget(evt.SourceEntityId.Value);
                SendCharCombatTargets(evt.SourceEntityId.Value);
            }
        });

        _eventBus.Subscribe("combat.end", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCharCombatTarget(evt.SourceEntityId.Value);
            SendCharCombatTargets(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("combat.kill", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCharCombatTarget(evt.SourceEntityId.Value);
            SendCharCombatTargets(evt.SourceEntityId.Value);

            // Update Room.Nearby for all players in the room (mob just died)
            var killer = _world.GetEntity(evt.SourceEntityId.Value);
            if (killer?.LocationRoomId != null)
            {
                var room = _world.GetRoom(killer.LocationRoomId);
                if (room != null)
                {
                    foreach (var occupant in room.Entities.Where(e => e.Type == "player"))
                    {
                        var occupantSession = _sessions.GetByEntityId(occupant.Id);
                        if (occupantSession == null) { continue; }
                        if (!_handlers.TryGetValue(occupantSession.Connection.Id, out var h)) { continue; }
                        SendRoomNearby(h, occupant);
                    }
                }
            }
        });

        _eventBus.Subscribe("ability.used", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            MarkVitalsDirty(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.item.picked_up", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCharItems(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.item.dropped", evt =>
        {
            if (!evt.SourceEntityId.HasValue) { return; }
            SendCharItems(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.item.given", evt =>
        {
            if (evt.SourceEntityId.HasValue)
            {
                SendCharItems(evt.SourceEntityId.Value);
            }
            if (evt.TargetEntityId.HasValue)
            {
                SendCharItems(evt.TargetEntityId.Value);
            }
        });

        _eventBus.Subscribe("entity.equipped", evt =>
        {
            if (!evt.SourceEntityId.HasValue)
            {
                return;
            }
            SendCharEquipment(evt.SourceEntityId.Value);
            SendCharItems(evt.SourceEntityId.Value);
        });

        _eventBus.Subscribe("entity.unequipped", evt =>
        {
            if (!evt.SourceEntityId.HasValue)
            {
                return;
            }
            SendCharEquipment(evt.SourceEntityId.Value);
            SendCharItems(evt.SourceEntityId.Value);
        });
    }

    public void RegisterHandler(string connectionId, IGmcpHandler handler)
    {
        _handlers[connectionId] = handler;
    }

    public void UnregisterHandler(string connectionId)
    {
        _handlers.TryRemove(connectionId, out _);
    }

    public void SendRaw(string connectionId, string package, object payload)
    {
        if (!_handlers.TryGetValue(connectionId, out var handler)) { return; }
        if (!handler.GmcpActive) { return; }
        handler.Send(package, payload);
    }

    public void SendLoginPrompt(string connectionId, string prompt)
    {
        if (!_handlers.TryGetValue(connectionId, out var handler)) { return; }
        if (!handler.GmcpActive) { return; }
        handler.Send("Login.Prompt", new { prompt });
    }

    public void SendLoginPhase(string connectionId, string phase)
    {
        if (!_handlers.TryGetValue(connectionId, out var handler)) { return; }
        if (!handler.GmcpActive) { return; }
        handler.Send("Char.Login.Phase", new { phase });
    }

    public void Send(Guid entityId, string package, object payload)
    {
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }

        if (_handlers.TryGetValue(session.Connection.Id, out var handler))
        {
            handler.Send(package, payload);
        }
    }

    public bool SupportsPackage(Guid entityId, string package)
    {
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return false; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return false; }
        return handler.SupportsPackage(package);
    }

    public void MarkVitalsDirty(Guid entityId)
    {
        lock (_dirtyLock) { _dirtyVitals.Add(entityId); }
    }

    public void FlushDirtyVitals()
    {
        Guid[] dirty;
        lock (_dirtyLock)
        {
            if (_dirtyVitals.Count == 0) { return; }
            dirty = _dirtyVitals.ToArray();
            _dirtyVitals.Clear();
        }

        foreach (var entityId in dirty)
        {
            var entity = _world.GetEntity(entityId);
            if (entity == null) { continue; }

            Send(entityId, "Char.Vitals", new
            {
                hp = entity.Stats.Hp,
                maxhp = entity.Stats.MaxHp,
                mana = entity.Stats.Resource,
                maxmana = entity.Stats.MaxResource,
                mv = entity.Stats.Movement,
                maxmv = entity.Stats.MaxMovement
            });
        }
    }

    public void OnPlayerLoggedIn(string connectionId, Entity entity)
    {
        SendLoginPhase(connectionId, "playing");
        SendPostLoginBurst(connectionId, entity);
    }

    public void SendPostLoginBurst(string connectionId, Entity entity)
    {
        if (!_handlers.TryGetValue(connectionId, out var handler)) { return; }
        if (!handler.GmcpActive) { return; }

        SendWorldDisplayColors(handler);
        SendCharStatusVars(handler);
        SendCharStatus(handler, entity);
        SendCharVitals(handler, entity);
        SendCharExperience(handler, entity);
        SendCharCommands(handler, entity);
        SendCharEffects(handler, entity);
        SendRoomInfo(handler, entity);
        SendRoomNearby(handler, entity);
        SendWorldTime(handler);
        SendWorldWeather(handler, entity);
        SendCharItems(handler, entity);
        SendCharEquipment(handler, entity);
    }

    private static void SendCharStatusVars(IGmcpHandler handler)
    {
        handler.Send("Char.StatusVars", new
        {
            hp = "Current HP",
            maxhp = "Max HP",
            mana = "Current Mana",
            maxmana = "Max Mana",
            mv = "Current Movement",
            maxmv = "Max Movement",
            name = "Character name",
            race = "Race",
            @class = "Class",
            level = "Level"
        });
    }

    private void SendCharStatus(IGmcpHandler handler, Entity entity)
    {
        var alignment = _alignmentManager.Get(entity.Id);
        var alignmentBucket = _alignmentManager.Bucket(entity.Id);
        var gold = entity.GetProperty<int>(CurrencyProperties.Gold);
        var hungerValue = entity.HasProperty(SustenanceProperties.Sustenance)
            ? entity.GetProperty<int>(SustenanceProperties.Sustenance)
            : 100;
        var hungerTier = _sustenanceConfig.GetTier(hungerValue);

        handler.Send("Char.Status", new
        {
            name = entity.Name,
            race = entity.GetProperty<string?>(CommonProperties.Race) ?? "",
            @class = entity.GetProperty<string?>(CommonProperties.Class) ?? "",
            level = _progressionManager.GetAllTracks()
                .Select(t => _progressionManager.GetLevel(entity.Id, t.Name))
                .DefaultIfEmpty(0)
                .Max(),
            str = entity.Stats.Strength,
            @int = entity.Stats.Intelligence,
            wis = entity.Stats.Wisdom,
            dex = entity.Stats.Dexterity,
            con = entity.Stats.Constitution,
            luk = entity.Stats.Luck,
            alignment,
            alignmentBucket,
            gold,
            hungerTier,
            hungerValue,
            isAdmin = entity.HasTag("admin"),
        });
    }

    private void SendCharExperience(IGmcpHandler handler, Entity entity)
    {
        var tracks = _progressionManager.GetAllTracks()
            .Select(t =>
            {
                var info = _progressionManager.GetTrackInfo(entity.Id, t.Name);
                if (info == null) { return null; }
                return new
                {
                    name = t.Name,
                    level = info.Level,
                    xp = info.Xp,
                    xpToNext = info.XpToNext,
                    currentLevelThreshold = info.CurrentLevelThreshold,
                };
            })
            .Where(t => t != null)
            .ToList();
        handler.Send("Char.Experience", new { tracks });
    }

    public void SendCharExperience(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }
        SendCharExperience(handler, entity);
    }

    private static void SendCharVitals(IGmcpHandler handler, Entity entity)
    {
        handler.Send("Char.Vitals", new
        {
            hp = entity.Stats.Hp,
            maxhp = entity.Stats.MaxHp,
            mana = entity.Stats.Resource,
            maxmana = entity.Stats.MaxResource,
            mv = entity.Stats.Movement,
            maxmv = entity.Stats.MaxMovement
        });
    }

    // Keyword-level overrides: applied last, win over category consolidation.
    // Used when a derived category mixes concerns (e.g. "commands" file has both
    // utility and combat commands).
    private static readonly Dictionary<string, string> KeywordCategoryOverrides =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["commands"] = "utility",
            ["consider"] = "combat",
            ["flee"]     = "combat",
            ["kill"]     = "combat",
            ["wimpy"]    = "combat",
        };

    private static string NormalizeCategory(string raw) => raw.ToLower() switch
    {
        // objects
        "close" or "open" or "lock" or "unlock" => "objects",
        // items
        "drink" or "eat" or "fill" or "quaff" or "recite" or "donate" => "items",
        // movement
        "enter" or "leave" => "movement",
        // utility
        "time" or "weather" or "information" => "utility",
        // progression
        "train" or "tree" or "practice" or "list" => "progression",
        _ => raw,
    };

    private void SendCharCommands(IGmcpHandler handler, Entity entity)
    {
        var commands = _commandRegistry.PrimaryKeywords
            .Select(kw => _commandRegistry.Resolve(kw))
            .Where(r => r != null)
            .Select(r => r!)
            .Where(r =>
            {
                if (r.VisibleTo == null) { return true; }
                try { return r.VisibleTo(entity); }
                catch { return false; }
            })
            .Select(r =>
            {
                var raw = !string.IsNullOrEmpty(r.Category) ? r.Category : DeriveCategory(r.SourceFile);
                var normalized = NormalizeCategory(raw);
                var category = KeywordCategoryOverrides.TryGetValue(r.Keyword, out var kw) ? kw : normalized;
                return new
                {
                    keyword = r.Keyword,
                    category,
                    description = r.Description,
                    aliases = r.Aliases,
                };
            })
            .OrderBy(c => c.category)
            .ThenBy(c => c.keyword)
            .ToList();

        handler.Send("Char.Commands", new { commands });
    }

    private static string DeriveCategory(string sourceFile)
    {
        if (string.IsNullOrEmpty(sourceFile)) { return "misc"; }
        var normalized = sourceFile.Replace('\\', '/');
        if (normalized.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["scripts/".Length..];
        }
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash <= 0) { return "misc"; }
        var fileName = normalized[(lastSlash + 1)..];
        var dotIndex = fileName.LastIndexOf('.');
        var stem = dotIndex >= 0 ? fileName[..dotIndex] : fileName;
        return string.IsNullOrEmpty(stem) ? "misc" : stem.ToLower();
    }

    private void SendRoomInfo(IGmcpHandler handler, Entity entity)
    {
        if (entity.LocationRoomId == null) { return; }
        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null) { return; }

        var exits = new Dictionary<string, string?>();
        var doors = new Dictionary<string, object>();

        foreach (var dir in room.AvailableExits())
        {
            var exit = room.GetExit(dir);
            if (exit == null) { continue; }

            var shortDir = dir.ToShortString().ToLower();
            exits[shortDir] = exit.TargetRoomId;

            if (exit.Door != null)
            {
                doors[shortDir] = new
                {
                    isClosed = exit.Door.IsClosed,
                    isLocked = exit.Door.IsLocked,
                };
            }
        }

        var payload = new
        {
            num = room.Id,
            name = room.Name,
            area = room.Area ?? "",
            description = room.Description,
            environment = room.GetProperty<string?>("terrain") ?? "",
            weatherExposed = room.WeatherExposed,
            timeExposed = room.TimeExposed,
            exits,
            doors = doors.Count > 0 ? (object)doors : null,
        };

        handler.Send("Room.Info", payload);
    }

    private void SendRoomNearby(IGmcpHandler handler, Entity entity)
    {
        if (entity.LocationRoomId == null) { return; }
        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null) { return; }

        var entities = room.Entities
            .Where(e => e.Id != entity.Id)
            .Where(e => e.Type is "player" or "npc" or "mob")
            .Select(e =>
            {
                var templateId = e.GetProperty<string?>("template_id");
                var tags = e.Tags.ToArray();
                var tier = HealthTier.Get(e.Stats.Hp, e.Stats.MaxHp);
                return new { name = e.Name, type = e.Type, templateId, tags, healthTier = tier.Tier };
            })
            .ToList();

        handler.Send("Room.Nearby", new { entities });
    }

    private void SendWorldTime(IGmcpHandler handler)
    {
        handler.Send("World.Time", new
        {
            hour     = _gameClock.CurrentHour,
            period   = _gameClock.CurrentPeriod.ToString().ToLower(),
            dayCount = _gameClock.DayCount,
        });
    }

    private void SendWorldWeather(IGmcpHandler handler, Entity entity)
    {
        if (entity.LocationRoomId == null) { return; }
        var room = _world.GetRoom(entity.LocationRoomId);
        if (room == null || room.Area == null) { return; }
        var state = _weatherService.GetCurrentWeather(room.Area);
        handler.Send("World.Weather", new { state });
    }

    private void SendWorldDisplayColors(IGmcpHandler handler)
    {
        var colors = _themeRegistry.GetHtmlMap();
        handler.Send("World.Display.Colors", new { colors });
    }

    public void SendRoomInfoForEntity(Guid entityId)
    {
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }

        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        SendRoomInfo(handler, entity);
    }

    public void SendCommChannel(Guid entityId, string channel, string sender, string text)
    {
        Send(entityId, "Comm.Channel", new { channel, sender, text });
    }

    public void SendRoomWrongDir(Guid entityId)
    {
        Send(entityId, "Room.WrongDir", "");
    }

    public void SendCharStatus(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }
        SendCharStatus(handler, entity);
    }

    public void SendCharEffects(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }
        SendCharEffects(handler, entity);
    }

    private void SendCharEffects(IGmcpHandler handler, Entity entity)
    {
        var active = _effectManager.GetActive(entity.Id);
        var effects = active.Select(e => new
        {
            id = e.Id,
            name = _abilityRegistry.Get(e.SourceAbilityId ?? "")?.Name ?? e.SourceAbilityId ?? e.Id,
            remainingPulses = e.RemainingPulses,
            flags = e.Flags,
            // "harmful" flag marks debuffs; pack authors set this on ActiveEffect.Flags
            type = e.Flags.Contains("harmful") ? "debuff" : "buff",
        }).ToList();

        handler.Send("Char.Effects", new { effects });
    }

    public void SendCharItems(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }
        SendCharItems(handler, entity);
    }

    private void SendCharItems(IGmcpHandler handler, Entity entity)
    {
        var items = entity.Contents
            .GroupBy(e => e.GetProperty<string?>("template_id") ?? e.Id.ToString())
            .Select(g =>
            {
                var first = g.First();
                var rarity = first.GetProperty<string?>(Engine.Items.ItemProperties.Rarity);
                var essence = first.GetProperty<string?>(Engine.Items.ItemProperties.Essence);
                return new
                {
                    id = first.Id.ToString(),
                    name = first.Name,
                    templateId = first.GetProperty<string?>("template_id"),
                    quantity = g.Count(),
                    rarity,
                    essence,
                    rarityTag = _rarityRegistry.FormatInline(rarity),
                    essenceTag = _essenceRegistry.Format(essence),
                };
            })
            .ToList();

        handler.Send("Char.Items", new { items });
    }

    public void SendCharEquipment(Guid entityId)
    {
        var entity = _world.GetEntity(entityId);
        if (entity == null) { return; }
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }
        SendCharEquipment(handler, entity);
    }

    private void SendCharEquipment(IGmcpHandler handler, Entity entity)
    {
        var slots = new Dictionary<string, object?>();
        foreach (var slotDef in _slotRegistry.AllSlots)
        {
            if (slotDef.Max == 1)
            {
                var equipped = entity.GetEquipment(slotDef.Name);
                slots[slotDef.Name] = BuildSlotPayload(equipped);
            }
            else
            {
                for (var i = 0; i < slotDef.Max; i++)
                {
                    var slotKey = $"{slotDef.Name}:{i}";
                    var equipped = entity.GetEquipment(slotKey);
                    slots[slotKey] = BuildSlotPayload(equipped);
                }
            }
        }
        foreach (var (slotKey, equipped) in entity.Equipment)
        {
            if (!slots.ContainsKey(slotKey))
            {
                slots[slotKey] = BuildSlotPayload(equipped);
            }
        }
        handler.Send("Char.Equipment", new { slots });
    }

    private object? BuildSlotPayload(Entity? equipped)
    {
        if (equipped == null) { return null; }
        var rarity = equipped.GetProperty<string?>(Engine.Items.ItemProperties.Rarity);
        var essence = equipped.GetProperty<string?>(Engine.Items.ItemProperties.Essence);
        return new
        {
            id = equipped.Id.ToString(),
            name = equipped.Name,
            rarity,
            essence,
            rarityTag = _rarityRegistry.FormatInline(rarity),
            essenceTag = _essenceRegistry.Format(essence),
        };
    }

    public void SendCharCombatTarget(Guid entityId)
    {
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }

        var targetId = _combatManager.GetPrimaryTarget(entityId);
        if (targetId == null)
        {
            handler.Send("Char.Combat.Target", new { active = false });
            return;
        }

        var target = _world.GetEntity(targetId.Value);
        if (target == null)
        {
            handler.Send("Char.Combat.Target", new { active = false });
            return;
        }

        var tier = HealthTier.Get(target.Stats.Hp, target.Stats.MaxHp);
        handler.Send("Char.Combat.Target", new
        {
            active = true,
            name = target.Name,
            healthTier = tier.Tier,
            healthText = tier.Text,
        });
    }

    public void SendCharCombatTargets(Guid entityId)
    {
        var session = _sessions.GetByEntityId(entityId);
        if (session == null) { return; }
        if (!_handlers.TryGetValue(session.Connection.Id, out var handler)) { return; }

        var combatList = _combatManager.GetCombatList(entityId);
        if (combatList.Count == 0)
        {
            handler.Send("Char.Combat.Targets", new { targets = Array.Empty<object>() });
            return;
        }

        var primaryId = _combatManager.GetPrimaryTarget(entityId);
        var targets = combatList
            .Select(id => _world.GetEntity(id))
            .Where(e => e != null)
            .Select(e =>
            {
                var t = HealthTier.Get(e!.Stats.Hp, e.Stats.MaxHp);
                return new
                {
                    id = e.Id.ToString(),
                    name = e.Name,
                    healthTier = t.Tier,
                    healthText = t.Text,
                    isPrimary = e.Id == primaryId,
                };
            })
            .ToList();

        handler.Send("Char.Combat.Targets", new { targets });
    }
}
