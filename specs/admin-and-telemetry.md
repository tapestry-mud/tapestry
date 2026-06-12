---
capability: admin-and-telemetry
last-updated: 2026-06-12
---

# Admin and Telemetry

## Overview

The admin subsystem exposes a JavaScript scripting surface (`tapestry.admin`,
`tapestry.watch`, `tapestry.help`) backed by C# modules. Admin commands are
pack-registered JavaScript handlers; the engine contributes one kernel command
(`badinput`). Telemetry is emitted via .NET System.Diagnostics.Metrics (meter
`"Tapestry"`) and System.Diagnostics.ActivitySource (source `"Tapestry"`).
Bad-input tracking accumulates unrecognized command verbs in memory and
increments an OTel counter per occurrence.

---

## Behavior

### Admin privilege gate

- All pack admin commands carry `admin: true` in their registration; the
  command router enforces role-gating before dispatch.
  [packs/@tapestry/core/scripts/commands/admin-teleport.js]
- The kernel `badinput` command is registered with `roles: ["admin"]`.
  [src/Tapestry.Server/Modules/BadInputModule.cs]
- Wizlock is a runtime-only flag on `WizlockState`. While set, the login flow
  refuses non-admin characters; already-connected players are unaffected.
  The flag is not persisted and resets to unlocked on reboot (ROM parity).
  [src/Tapestry.Engine/Login/WizlockState.cs, commit be2610e]
- JS surface: `tapestry.admin.setWizlock(bool)` / `tapestry.admin.isWizlocked()`.
  The pack `wizlock` command toggles the state.
  [packs/@tapestry/core/scripts/commands/admin-wizlock.js]

### Entity management: set

- `set` dispatches through `AdminModule.DispatchSet`. Settable targets are
  `player`, `npc`, `item`, and `room`.
  [src/Tapestry.Scripting/Modules/AdminModule.cs L193-289]
- For `player`, `npc`, and `item` the engine resolves the target from the
  calling admin's context: players by online session, NPCs from the admin's
  current room, items from the admin's inventory/equipment.
  [AdminModule.cs L541-689]
- Ordinal syntax (`2.keyword`) is supported for disambiguating multiple matches.
  [AdminModule.cs L553-558]
- For `room`, no target token is accepted; the admin's current room is the
  implicit target. [AdminModule.cs L235-259]
- Declared attributes (PropertyRegistry / TagRegistry entries marked
  `IsAdminSettable`) flow through `AttributeWriter`. Out-of-registry
  subsystem ops (stats, alignment, gold, npc hp, proficiency, training cap)
  are handled by retained pack-side domain handlers in `admin-set.js`.
  [packs/@tapestry/core/scripts/commands/admin-set.js L52-114]
- `set ?`, `set [kind] ?`, and `set [kind] [attr] ?` display discovery panels
  rendered via `PanelRenderer`. [AdminModule.cs L196-231]

### Entity management: grant

- `grant` dispatches through `AdminModule.DispatchGrant` to JS handlers
  registered via `tapestry.admin.grant.register({kind, type, handler, ...})`.
  Allowed kinds: `player`, `npc`, `item`, `room`.
  [AdminModule.cs L291-350]
- Duplicate `(kind, type)` keys log a warning; the later registration wins.
  [AdminModule.cs L186-190]
- Built-in grant types in core: `player xp`, `player train`, `player gold`.
  [packs/@tapestry/core/scripts/commands/admin-grant.js]
- `grantrole` / `revokerole` add or remove roles from online players via
  `tapestry.world.addRole`. Separate commands from `grant` to avoid keyword
  collision. [packs/@tapestry/core/scripts/commands/admin-grant-role.js]

### World manipulation: spawn, loaditem, purge, restore, peace

- `spawn <templateId>` spawns a mob from a template into the admin's current
  room. [packs/@tapestry/core/scripts/commands/admin-spawn.js]
- `loaditem <templateId>` instantiates an item template into the admin's
  inventory. [packs/@tapestry/core/scripts/commands/admin-loaditem.js]
- `purge [npc|items|all]` removes matching entities from the admin's room;
  defaults to all. [packs/@tapestry/core/scripts/commands/admin-purge.js]
- `restore [player|all]` refills vitals for a named player or all online
  players via `tapestry.stats.restoreVitals`.
  [packs/@tapestry/core/scripts/commands/admin-restore.js]
- `peace` removes all combatants in the admin's room from combat.
  [packs/@tapestry/core/scripts/commands/admin-peace.js]

### Teleport / goto

- `teleport <player> <roomId>` (aliases: `tp`) moves an online player to a
  room by id. Fails if the player is not online or the room id is unknown.
  [packs/@tapestry/core/scripts/commands/admin-teleport.js]

### Inspect and locate

- `inspect [entity]` shows full stats, properties, tags, equipment, inventory,
  alignment, proficiency for a visible entity in the admin's room.
  [packs/@tapestry/core/scripts/commands/admin-inspect.js]
- `inspect room [id]` shows name, description, area, biome, terrain, flags,
  properties, exits, and occupants for the given room (defaults to current).
  [admin-inspect.js L139-210]
- `inspect area [id]` shows area metadata including level range, reset interval,
  and provenance (pack-supplied, pack+edits, or hand-authored).
  [admin-inspect.js L212-234]
- `whereis <keyword>` scans all world entities; `mwhere` filters to NPCs;
  `owhere` filters to items and containers. Results cap at 100.
  [packs/@tapestry/core/scripts/commands/admin-whereis.js]

### Combat admin

- `peace` clears combat for all occupants in the admin's room (see above).
- `set npc hp <target> <value>` calls `admin.setEntityHp` which sets both
  base max HP and current HP simultaneously. [AdminModule.cs L94-102]

### executeAs seam

- `tapestry.admin.executeAs(entityId, commandLine)` dispatches a command line
  as the target entity through the standard parse+route path. Privilege is NOT
  escalated; output goes to the target's session. Session-backed players only;
  returns `false` for sessionless or invalid entities.
  [AdminModule.cs L111-133]
- This seam is the backing primitive for pack `force`/`at` commands.
  Documented as out-of-scope here; see command-dispatch.md.

### Watch / snoop

- `TeeConnection` is an output decorator inserted in the connection chain
  between `ColorRenderingConnection` and `WrappingConnection`. Every write is
  forwarded to the owning player and mirrored to all subscribed watchers.
  [src/Tapestry.Engine/Watch/TeeConnection.cs]
- `ShouldBroadcast` (default `true`) is a per-connection gate; a future
  producer may set it false for private writes. `WatchBroadcastScope.Suppressed`
  is a per-write suppression seam (Slice C). [TeeConnection.cs L23, L49]
- Watcher sinks resolve live on each broadcast; a reconnected watcher (new
  connection, same entity id) keeps receiving output without re-subscribing.
  [src/Tapestry.Scripting/Modules/WatchModule.cs L43-51, commit 70db6ab]
- Self-subscription is rejected to prevent infinite recursion through the tee.
  [WatchModule.cs L39]
- Admins may not snoop other admins; the `snoop` command checks the target's
  roles before calling `tapestry.watch.start`.
  [packs/@tapestry/core/scripts/commands/snoop.js L40-46]
- JS surface: `tapestry.watch.start(watcherEntityId, targetEntityId)` and
  `tapestry.watch.stop(watcherEntityId)`.
  [WatchModule.cs L29-58]
- Full output pipeline ownership of TeeConnection is documented in
  output-pipeline.md.

### Help system -- HelpTopic model

- A `HelpTopic` carries: `id`, `title`, `category`, `brief`, `body`,
  `syntax[]`, `keywords[]`, `see_also[]`, optional `role`, and `override`.
  [src/Tapestry.Shared/Help/HelpTopic.cs]
- Topics are namespaced as `{packName}:{id}`; both bare and namespaced ids are
  valid lookup keys. [HelpTopic.cs L28, HelpService.cs L125-126]
- The `role` field gates visibility: topic is visible only to entities whose
  highest role tier is >= the topic's role (hierarchy: player < builder <
  admin). A nil role is visible to all including pre-login (chargen).
  [src/Tapestry.Engine/Help/HelpService.cs L207-215]

### Help system -- registration (HelpSeal)

- Packs supply help files as `*.yaml` under `{packRoot}/help/`. Files are
  loaded via `HelpService.LoadPack` and routed through `RegistrationPolicy`
  (Kind "help") so cross-pack same-id collisions produce boot errors unless
  one side declares `{ override: true }` with a dependency edge.
  [HelpService.cs L60-121]
- After `RegistrationPolicy.Resolve()`, `HelpSeal.Seal()` runs two passes:
  (1) command-shadowing authority -- a hand-authored topic whose id matches
  a resolved command owned by a different pack must declare `override: true`
  and a dependency edge; engine/kernel-owned commands are exempt;
  (2) auto-gen gap-fill -- `CommandHelpGenerator.GenerateGaps` generates a
  topic for every resolved command that has `ArgDefinitions` and no
  winning hand-authored topic.
  [src/Tapestry.Engine/Help/HelpSeal.cs L36-82]

### Help system -- command-derived help generation

- `CommandHelpGenerator.GenerateFor` returns null if a registration has no
  `ArgDefinitions`; otherwise it builds a topic with a syntax line from the
  arg definitions. [src/Tapestry.Engine/CommandHelpGenerator.cs L8-28]
- Syntax format: required args render as `[arg]`, optional as `([arg])`, bulk
  as `[arg | all | all.arg]`. Prepositions are inserted before the placeholder.
  [CommandHelpGenerator.cs L51-69, tests: CommandHelpGeneratorTests.cs]

### Help system -- lookup (HelpService)

- `Query` resolves in order: exact id match -> exact title match ->
  fuzzy (title/keyword substring). A single fuzzy match returns `"ok"`;
  multiple fuzzy matches return `"multiple"` with a summary list; no match
  returns `"no_match"`. [HelpService.cs L135-167]
- Lookup is case-insensitive throughout. [HelpServiceTests.cs L54-59]
- Load-order no longer breaks ties; the last `AddTopic` call for a given id
  wins (RegistrationPolicy is the cross-pack authority).
  [HelpService.cs L192-201, HelpServiceTests.cs L62-77, commit 90c4eb4]
- `List(entityId, category)` and `Categories(entityId)` apply the same
  role-visibility filter as `Query`. [HelpService.cs L170-188]

### Help system -- pack JS surface (HelpModule)

- `tapestry.help.query(entityId?, term)` -> `{status, topic?|matches?|term?}`
- `tapestry.help.list(entityId?, category)` -> `[{id, title, brief}]`
- `tapestry.help.categories(entityId?)` -> `[string]`
  [src/Tapestry.Scripting/Modules/HelpModule.cs]
- When the first argument is a GUID it is treated as the player context and
  the second argument is the term; otherwise the first argument is the term
  and the context is anonymous. [HelpModule.cs L111-123]

### Bad-input tracking

- Every unrecognized command verb is passed to `BadInputTracker.Record`. The
  tracker accumulates a `ConcurrentDictionary` keyed on the normalized
  (lowercased) verb; each entry stores count, first-seen, and last-seen
  timestamps. [src/Tapestry.Engine/BadInputTracker.cs]
- Each call increments the `tapestry_bad_input_total` OTel counter with a
  `verb` tag and emits a structured `LogInformation` line with verb, full
  input, player name, and room id. [BadInputTracker.cs L32-36]
- The kernel `badinput` admin command lists entries sorted by count descending,
  or clears the log with `badinput clear`. There is no threshold-based
  automatic action; the tracker is purely observational.
  [src/Tapestry.Server/Modules/BadInputModule.cs]

### Telemetry -- meter and tracing source

- Meter name: `"Tapestry"`, version `"1.0.0"`.
  [src/Tapestry.Engine/TapestryMetrics.cs L7]
- ActivitySource name: `"Tapestry"`, version `"1.0.0"`.
  [src/Tapestry.Engine/TapestryTracing.cs L7-9]

### Telemetry -- defined metrics

Tick / command throughput:
- `tapestry.tick.duration_ms` Histogram[double] -- time per tick phase (ms)
- `tapestry.tick.commands_processed` Counter[long] -- commands processed per tick
- `tapestry.tick.events_processed` Counter[long] -- system events per tick
- `tapestry.tick.handler_wall_ms` Histogram[double] -- per-handler wall-clock time, tagged by handler
- `tapestry.tick.handler_cpu_ms` Histogram[double] -- per-handler thread CPU time, tagged by handler
- `tapestry.command.duration_ms` Histogram[double] -- execution time per command handler (ms)
- `tapestry.input_queue.depth` Histogram[long] -- input queue depth sampled each tick

Connection and session:
- `tapestry.connections.active` UpDownCounter[long] -- current active connections
- `tapestry.session.duration_s` Histogram[double] -- session duration on disconnect (s)
- `tapestry_linkdead_active` UpDownCounter[long] -- currently link-dead sessions
- `tapestry_linkdead_reconnected` Counter[long] -- successful reconnections after link-dead
- `tapestry_linkdead_expired` Counter[long] -- link-dead sessions that timed out and were despawned

Flood protection and bad input:
- `tapestry_flood_commands_dropped` Counter[long] -- commands rejected by token bucket
- `tapestry_flood_disconnects` Counter[long] -- players disconnected for command flooding
- `tapestry_bad_input_total` Counter[long] -- unrecognized command frequency, tagged by `verb`

Mob AI:
- `tapestry.mob_ai.phase_ms` Histogram[double] -- mob-AI time per phase per invocation, tagged by phase
- `tapestry.mob_ai.budget_exhausted` Counter[long] -- ticks where the AI sweep stopped early on budget
- `tapestry.mob_ai.invocation_cap` Counter[long] -- behavior invocation-cap strikes, tagged by behavior
- `tapestry.mob_ai.quarantine` Counter[long] -- behavior quarantine events, tagged by behavior and pack
- `tapestry.mob_ai.cursor_lag` ObservableGauge[int] -- mobs deferred by tick budget in the latest sweep
  (registered separately via `RegisterMobAiCursorLag`)

World census (registered via `RegisterWorldCensus`, pull-based at scrape time):
- `tapestry.world.entities` ObservableGauge[int] -- live entity count by `type` tag
- `tapestry.world.tag_index_tags` ObservableGauge[int] -- distinct tag keys in the world tag index
- `tapestry.world.tag_index_entries` ObservableGauge[long] -- total entity-tag memberships
- `tapestry.world.properties_total` ObservableGauge[long] -- sum of property-bag entries across all entities
- `tapestry.world.max_entity_properties` ObservableGauge[int] -- largest single entity property-bag size

World census gauges never throw into the export path; if the sampler races and
returns null, that scrape emits no data (a gap, not a misleading zero).
[TapestryMetrics.cs L120-163]

[src/Tapestry.Engine/TapestryMetrics.cs]

---

## Rejected and Reverted

- **admin.restoreVitals JS seam** -- added in commit c0696c7, reverted in
  e1697f0 (`revert: drop duplicate admin.restoreVitals seam`). Packs must use
  `tapestry.stats.restoreVitals` directly.

---

## Change Log

| Change Record | Summary |
|---------------|---------|

---

## Sources consulted

- src/Tapestry.Scripting/Modules/AdminModule.cs (751 lines, full read)
- src/Tapestry.Engine/BadInputTracker.cs
- src/Tapestry.Engine/TapestryMetrics.cs (full read)
- src/Tapestry.Engine/TapestryTracing.cs
- src/Tapestry.Engine/Help/HelpService.cs
- src/Tapestry.Engine/Help/HelpSeal.cs
- src/Tapestry.Engine/CommandHelpGenerator.cs
- src/Tapestry.Engine/Ui/HelpRenderer.cs
- src/Tapestry.Shared/Help/HelpTopic.cs
- src/Tapestry.Shared/Help/HelpTopicSummary.cs
- src/Tapestry.Scripting/Modules/HelpModule.cs
- src/Tapestry.Server/Modules/BadInputModule.cs
- src/Tapestry.Engine/Watch/TeeConnection.cs
- src/Tapestry.Scripting/Modules/WatchModule.cs
- src/Tapestry.Engine/Login/WizlockState.cs
- packs/@tapestry/core/scripts/commands/admin-*.js (all 20 files)
- packs/@tapestry/core/scripts/commands/snoop.js
- tests/Tapestry.Engine.Tests/CommandHelpGeneratorTests.cs
- tests/Tapestry.Engine.Tests/Help/HelpServiceTests.cs
- git log --oneline -15 -- AdminModule.cs TapestryMetrics.cs
- git log --oneline -10 -- Help/ CommandHelpGenerator.cs BadInputModule.cs BadInputTracker.cs TapestryTracing.cs
- git log --oneline -8 -- Watch/ WatchModule.cs

## UNVERIFIED count: 0

All behavior bullets are anchored to a file or commit. No uncertain claims
were introduced.

## Out-of-scope notes

- executeAs seam: mentioned in context but documented in command-dispatch.md.
- TeeConnection output pipeline position (ColorRendering -> Tee -> Wrapping):
  noted here as a seam reference; full chain documented in output-pipeline.md.
- Session lifecycle (link-dead timers, despawn logic): sessions-and-connections.md.
- Login enforcement of wizlock (WizlockGate): login-and-accounts.md.
- HelpRenderer terminal formatting details are in scope only as a reference
  (CRLF, 78-column width); output-pipeline.md owns the rendering stack.
- `admin-at.js`, `admin-force.js`: command-dispatch.md (executeAs seam).
