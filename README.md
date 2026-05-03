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

A browser-based client lives in [`client/`](client/). Connect via browser -- no plugins required.

## Accessibility

Tapestry treats accessibility as a first-class concern. The web client includes:

- **Skip navigation** -- links to jump directly to game output, command input, or announcement settings
- **Screen reader announcements** -- live regions that announce game state changes (vitals warnings, combat events, chat messages, room changes) without requiring navigation away from the command input
- **Configurable announcement priority** -- each category can be set to Interrupt (assertive), Polite, or Off via the settings panel. Preferences persist per-browser
- **Vitals threshold alerts** -- announces when HP, mana, or movement drop below 40% (low) and 10% (critical), then stays silent until the player heals above the low threshold
- **xterm screen reader mode** -- terminal output is mirrored to a live text buffer so screen readers can read game output as it arrives
- **Semantic markup** -- proper landmarks (`<main>`, `role="log"`, `role="complementary"`), labeled inputs, and ARIA attributes throughout
- **Mobile layout** -- on small screens, GMCP panels are visually hidden but remain in the DOM for assistive technology

The telnet server works with any screen reader out of the box since it's plain text over a terminal connection.

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

## Current Status

### What's Built

**Core Engine:** Telnet + WebSocket server, ANSI color themes, GMCP protocol, tick-based game loop, event bus with cancellation, command registry with pack override, OpenTelemetry observability.

**Content System:** YAML + JavaScript content packs with load ordering and version constraints. Pack-defined equipment slots, rarity tiers, progression tracks, class paths, races.

**World:** Rooms with directional + keyword exits, doors (lock/pick/key), temporary portals with expiry, weather zones, day/night cycle, area resets, spawn tables with rare spawns.

**Entities:** Unified entity model with tags + dynamic properties. Stats with equipment modifiers. Weight-based inventory. Multi-slot equipment. Containers (put/get/fill). Consumables (eat/drink/quaff/recite). Rest/sleep with regen multipliers.

**Combat:** D20 hit resolution, 4-type AC (slash/pierce/bash/exotic), damage verb scaling (20 tiers), death with corpse/loot, flee, wimpy, alignment shifts.

**Progression:** XP tracks with configurable formulas, death penalty, level-up callbacks. Proficiency tiers (Novice through Master). Trainer NPCs. Class paths with auto-grant on level-up. Stat training at level-up.

**Social:** Say/yell/emote, communication channels, groups (follow/invite/kick/promote), XP and gold sharing, rescue, group chat.

**NPCs:** Wander/patrol/stationary AI, aggro, flee threshold, equipment drops, loot tables, shops (buy/sell/list).

**Character Creation:** Step-based wizard with ANSI panels, race/class/alignment selection, per-option lore text, validation rules.

**Web Client:** React 19 + TypeScript browser client. Three-column drag-and-drop layout. Auto-mapper. Real-time vitals, stats, effects, combat target, XP tracking via GMCP. Four themes. Screen reader support with configurable announcements.

## Contributing

The [issue tracker](https://github.com/tapestry-mud/tapestry/issues) is where all planned work lives. Issues are labeled by area (`server`, `client`, `pack`) and difficulty (`good first issue`).

To contribute: pick an issue, leave a comment, and open a PR against `master`.

## Why Tapestry

Most MUD engines hardcode game logic. Tapestry doesn't. The engine provides systems (entities, rooms, combat, progression, events) and packs provide the game (what a sword is, what a goblin does, what happens when you level up).

**What this means for contributors:**
- Build an entire game without touching C#. YAML + JavaScript is all you need
- Stack multiple packs. Your cyberpunk pack and someone's fantasy pack can coexist on the same server
- Override anything. A pack can replace the `look` command, redefine equipment slots, or add new stat types
- 37 scripting API modules with full engine access from JavaScript
- Modern tooling: OpenTelemetry observability, browser client with GMCP, comprehensive test suite

## License

[AGPL-3.0](LICENSE) — use it for anything, run it as a service, but keep your modifications open.
