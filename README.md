# Tapestry

A modular MUD engine where the engine provides the platform and content packs provide the game.

Think Minecraft's relationship to mods: the engine is the canvas, packs are the paint. A server operator loads the packs they want and gets a unique MUD. A content creator builds a pack and shares it with the community.

## Quick Start

```bash
dotnet run --project src/Tapestry.Server
```

Then connect with any telnet client:

```bash
telnet localhost 4000
```

Enter a name, and you're in. Type `help` for commands.

### Web Client

A browser-based client lives in [`client/`](client/). See the [Web Client Roadmap](client/ROADMAP.md) for its own phase tracking.

## Architecture

**Modular monolith** in C#/.NET 10. The engine has zero hardcoded game logic — everything comes from content packs.

```
Tapestry.Shared       Enums, interfaces, shared types
Tapestry.Engine       Entity system, rooms, world graph, event bus, command routing, game loop
Tapestry.Networking   Telnet server, ANSI color support
Tapestry.Scripting    Jint JS runtime, YAML loader, pack loader, JS-to-engine bridge
Tapestry.Data         Server configuration (YAML)
Tapestry.Server       Host startup, DI wiring
```

### Content Packs

Packs live in `packs/` and contain:

- **YAML** for data (rooms, areas, entities, items, mobs, loot tables, spawns)
- **JavaScript** for behavior (commands, event hooks, tick handlers, class definitions)
- **pack.yaml** manifest with metadata, version constraints, and content globs

Packs stack. `load_order` controls precedence, so a pack can override commands, extend areas, or replace items from lower-priority packs. The included `tapestry-core` pack provides a starter town and base commands. Additional packs layer new worlds on top, overriding and extending as needed.

### Key Tech

- **Jint** -- embedded JS runtime (ES6+) for pack scripting, 37 API modules
- **YamlDotNet** -- content file parsing
- **Event Bus** -- priority-ordered pub/sub with cancellation (scripts can veto actions)
- **Command Registry** -- priority-based dispatch with aliases (packs override commands)
- **Tick-based Game Loop** -- configurable rate, processes input queues and tick handlers
- **GMCP** -- structured data protocol for rich clients (vitals, room info, effects, combat)
- **WebSocket** -- native WebSocket server for browser-based play
- **OpenTelemetry** -- structured logging, metrics, and distributed tracing

## Configuration

`server.yaml` at the project root:

```yaml
server:
  name: "Tapestry Dev Server"
  telnet_port: 4000
  tick_rate_ms: 100

packs:
  - tapestry-core
```

## Creating a Content Pack

```
packs/my-pack/
  pack.yaml              # name, version, content globs
  areas/**/*.yaml        # room definitions
  scripts/init.js        # runs first — setup
  scripts/commands/*.js  # command registrations
```

The JS API available to scripts:

```javascript
// Register a command
tapestry.commands.register({
    name: 'wave',
    aliases: ['wav'],
    handler: function(player, args) {
        player.send('You wave enthusiastically.\r\n');
        tapestry.world.sendToRoomExcept(player.roomId, player.entityId,
            player.name + ' waves enthusiastically.\r\n');
    }
});

// Subscribe to events
tapestry.events.on('player.connect', function(event) { /* ... */ });

// World helpers
tapestry.world.moveEntity(entityId, direction)
tapestry.world.sendRoomDescription(entityId)
tapestry.world.getOnlinePlayers()
```

## Observability

The engine includes a full OpenTelemetry pipeline — structured logging (Serilog), metrics, and distributed tracing. It's entirely additive: the engine runs fine without the observability stack.

**Stack:**

| Component | Purpose | Image |
|-----------|---------|-------|
| OTel Collector | Pipeline hub — receives all telemetry from the engine | `otel/opentelemetry-collector-contrib` |
| Loki | Log storage | `grafana/loki` |
| Prometheus | Metrics storage (scrapes OTel Collector) | `prom/prometheus` |
| Jaeger | Trace storage | `jaegertracing/jaeger` |
| Grafana | Single UI for logs, metrics, and traces | `grafana/grafana` |

**Quick start:**

```bash
docker-compose up -d          # Start observability stack
# Edit server.yaml → telemetry.enabled: true
dotnet run --project src/Tapestry.Server
```

Then open **Grafana** at `http://localhost:3001` — the Tapestry Overview dashboard is pre-provisioned with:
- Active connections, commands/sec, events/sec gauges
- Tick and command duration percentile graphs
- Input queue depth
- Live log tail (filterable by level)
- Recent traces (clickable)

**Other ports** (for direct access if needed):
- Prometheus: `http://localhost:9091`
- Loki: `http://localhost:3100`

**What's instrumented:**
- Every GameLoop tick (event processing, command execution, tick handlers)
- Per-command execution time, tagged by command name and player
- Connection lifecycle (connect, login, disconnect with reason)
- Slow tick detection with in-game admin broadcast
- Metrics: tick duration, commands/sec, queue depth, active connections

```bash
docker-compose down            # Stop the stack — engine keeps running
```

## Tests

```bash
dotnet test
```

1058 tests across engine, scripting, and networking.

## Current Status

### What's Built

**Core Engine:** Telnet + WebSocket server, ANSI color themes, GMCP protocol, tick-based game loop, event bus with cancellation, command registry with pack override, OpenTelemetry observability. 1058 tests.

**Content System:** YAML + JavaScript content packs with load ordering and version constraints. Pack-defined equipment slots, rarity tiers, progression tracks, class paths, races.

**World:** Rooms with directional + keyword exits, doors (lock/pick/key), temporary portals with expiry, weather zones, day/night cycle, area resets, spawn tables with rare spawns.

**Entities:** Unified entity model with tags + dynamic properties. Stats with equipment modifiers. Weight-based inventory. Multi-slot equipment. Containers (put/get/fill). Consumables (eat/drink/quaff/recite). Rest/sleep with regen multipliers.

**Combat:** D20 hit resolution, 4-type AC (slash/pierce/bash/exotic), damage verb scaling (20 tiers), death with corpse/loot, flee, wimpy, alignment shifts.

**Progression:** XP tracks with configurable formulas, death penalty, level-up callbacks. Proficiency tiers (Novice through Master). Trainer NPCs. Class paths with auto-grant on level-up. Stat training at level-up.

**Social:** Say/yell/emote, communication channels, groups (follow/invite/kick/promote), XP and gold sharing, rescue, group chat.

**NPCs:** Wander/patrol/stationary AI, aggro, flee threshold, equipment drops, loot tables, shops (buy/sell/list).

**Character Creation:** Step-based wizard with ANSI panels, race/class/alignment selection, per-option lore text, validation rules.

**Web Client:** React 19 + TypeScript browser client. Three-column drag-and-drop layout. Auto-mapper. Real-time vitals, stats, effects, combat target, XP tracking via GMCP. Four themes.

### Roadmap

The core engine is feature-complete -- rooms, exits, combat, leveling, persistence, skills, spells, classes, races, alignment, character creation, shops, consumables, doors/portals, channels, weather/time, mob AI, groups/parties, GMCP/MSSP, and a full web client.

**Planned:**

| System | Notes |
|--------|-------|
| Essence / Enchantment | Item augmentation and progression |
| UI Polish | Command list, help system, prompt customization |
| Quests | NPC quest givers, task tracking, rewards |
| Combat balance | Tuning pass across all combat systems |
| Area Editor | In-game builder commands + visual editor in the web client |

**Good First Issues:**

| System | Notes |
|--------|-------|
| Biome & Visibility | YAML-defined biome types (cave, forest, city) on rooms. Biomes control darkness, light source requirements, map symbols. Complements areas (areas = WHERE, biomes = WHAT KIND) |
| Buff Spec YAML | Standalone buff definitions with trigger rates, trigger counts, flags (cancel-on-combat, cancel-on-action), permabuffs from race/equipment |
| Mob Command AI | `idle_commands`/`combat_commands` arrays on mob YAML. Mobs execute game commands (emote, say, wander) as their AI -- no JavaScript needed for flavor behavior |
| Room Atmosphere | Idle messages (ambient flavor text), signs (readable objects), exit messages (flavor text on traversal) |
| Secret Exits | `secret: true` on exits, hidden from `look` until discovered via search or skill check |
| Elemental Damage | Fire/ice/lightning/etc on weapons and spells, resistance properties on armor |
| NPC Conversations | Branching YAML-defined dialogue trees for NPCs |
| Advanced Protocols | MCCP2 (zlib compression), MXP (clickable links/sends), TLS port for encrypted connections |
| Telnet Map | ASCII auto-map from room graph (web client already has one) |
| Copyover | Graceful server restart without disconnecting players |

## Why Tapestry

Most MUD engines hardcode game logic. Tapestry doesn't. The engine provides systems (entities, rooms, combat, progression, events) and packs provide the game (what a sword is, what a goblin does, what happens when you level up).

**What this means for contributors:**
- Build an entire game without touching C#. YAML + JavaScript is all you need
- Stack multiple packs. Your cyberpunk pack and someone's fantasy pack can coexist on the same server
- Override anything. A pack can replace the `look` command, redefine equipment slots, or add new stat types
- 37 scripting API modules with full engine access from JavaScript
- Modern tooling: OpenTelemetry observability, browser client with GMCP, 1058 tests

### Tech Debt (Deferred)
- **SendToRoom O(n sessions)** -- Room-indexed session lookup when player count demands it
- **World.GetEntity fallback scan** -- Masks incomplete entity tracking, low frequency
- **EventBus reentrant locking** -- Theoretical contention under high throughput, no issues observed

## License

[AGPL-3.0](LICENSE) — use it for anything, run it as a service, but keep your modifications open.
