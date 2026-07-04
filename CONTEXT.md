# Tapestry Domain Glossary

The shared language of the Tapestry engine. This file is a glossary and nothing else:
behavior lives in `specs/`, design history lives in change records. Read this before
working in an unfamiliar system, and use these terms exactly - several core words are
overloaded, and the [Flagged ambiguities](#flagged-ambiguities) section is the decoder.

## The loop and time

**Tick**:
One iteration of the single-threaded game loop, default 100 ms (10 ticks/sec). All game
logic runs on this thread.

**Tick handler**:
A named periodic action registered with an interval and committed at the seal. Kernel-only;
pack JS schedules work via `schedule.every`, never by registering tick handlers.

**Pulse**:
One firing of an `IPulseHandler`, denominated by that handler's cadence - a combat pulse is
20 ticks, the swell clock pulses every tick. Ability `PulseDelay` and effect
`RemainingPulses` count combat pulses.

**Area tick**:
The per-area slow interval event (`area.tick`), stretched by `OccupiedModifier` while
players are present. Not a game tick.

**Area reset**:
One repopulation pass over a room's spawn rules, triggered by an area tick.

**Repop**:
The recurring behavior of area resets over time. `reset_interval <= 0` turns it off, so the
area populates once at boot and never again (how oracle areas work).

## World model

**World**:
The central in-memory store of all live game objects: a room index, an entity index, and a
copy-on-write tag index.
_Avoid_: game state, world state

**Entity**:
The universal runtime object. Players, mobs, items, and corpses are all entities,
distinguished by a `Type` string and carrying a property bag, tags, keywords, and roles.
_Avoid_: game object, actor (an "actor" is specifically the entity executing a command)

**Property bag**:
An entity's dictionary of named properties; setting a key to null removes it. The
`PropertyRegistry` decides which properties persist and which are transient.

**Tag**:
A case-insensitive marker on an entity or room, feeding the world tag index. Engine tags
are bare snake_case; pack tags are `packName:name` and cannot shadow engine tags.

**Keyword**:
A player-facing targeting token on an entity ("get sword"). Not a tag - keywords resolve
commands, tags drive game logic.

**Role**:
An entity descriptor with two disjoint kinds: actor-type roles (`player`, `mob`) and
privilege roles (`admin`, `builder`). Only privilege roles may gate command dispatch.

**Template**:
The blueprint an entity was spawned from, recorded in its `template_id` property.

**Area**:
A metadata-only descriptor (level range, reset interval, weather zone). Areas own no rooms;
rooms point at their area.

**Room**:
A world node keyed `namespace:key`, holding exits and its own tag and property bags. Only
`World.RekeyRoom` may change a room's id.
_Avoid_: room key and room id interchangeably - say "room id"

**Stub exit**:
A placeholder exit with an empty target that survives side-car reload. Moving through one
lazily mints the neighbor room via the `StubExitResolver` ("lazy-mint movement").

**Side-car**:
A runtime-written YAML file overlaying pack content (authored rooms, areas, item templates,
oracle tables). Connection exits never persist into side-cars.
_Avoid_: save file, overlay file

**Connection record**:
A cross-area exit stored under `connections/` and applied after rooms load. Created by the
`link` command; never written into a room side-car.

**Provenance**:
A room or area's origin classification: `[pack]`, `[authored]`, `[pack +edits]`, plus
`(orphaned)` for a dangling authored boundary.

## Vitals

**VitalsService**:
The single publishing write path for an entity's typed vitals (hp, resource, movement).
`Apply`/`Set`/`RestoreToMax` clamp the stat and publish `entity.vital.changed` when the
value actually changes; `Initialize` sets a spawn/load baseline without publishing.
`StatBlock`'s vital setters are private, so every gameplay write must go through it.

**entity.vital.changed**:
The event `VitalsService` publishes on every vital write that changes the value, carrying
`{ vital, old, new, delta, reason }`. GMCP's vitals batching and combat target-bar refresh
subscribe to this one topic instead of the pre-migration scatter of `ability.used`,
`entity.regen`, `entity.vital.depleted`, and `combat.hit`. Distinct from
`entity.vital.depleted`, which still fires separately when a vital reaches zero and feeds
the death pipeline.

## Property observability

**Observable property**:
A property-bag key declared reachable by an external consumer via
`PropertyRegistry.RegisterObservable(key, topic)`. Independent of the type registry, so it
can mark a pack-owned key the kernel never type-registered (e.g. `sustenance`).
`TryGetObservableTopic` is the read side; `EntityStatusBroadcaster` consults it from
`Entity.SetProperty` to publish `entity.<topic>.changed`.

**entity.status.changed**:
The event `EntityStatusBroadcaster` publishes when an observable property changes on the
`status` topic, carrying `{ key, old, new }`. `CommonProperties` declares `sustenance` and
`alignment` observable on this topic; `CharStatusHandler` subscribes and resends
`Char.Status`. Sibling to `entity.vital.changed` but covers the general property bag, not
the typed vitals store.

## Packs and the seal

**Pack**:
A distributable content bundle (`@scope/name`) with a `pack.yaml` manifest (snake_case
keys). The server loads only packs listed in `server.yaml`.
_Avoid_: plugin, addon, module (a "module" is an ES module, never a pack)

**Namespace form**:
A pack id flattened for prefixing (`@tapestry/core` becomes `tapestry-core`). Every loaded
entity id must carry its owning pack's namespace prefix.

**Two-phase loading**:
Boot loads declarations (tags, properties, slots) for all packs alphabetically, then
content in dependency-topological order.

**Seal**:
The one-time boot moment when `RegistrationPolicy.Resolve()` arbitrates all accumulated
registration candidates and commits winners. "Post-seal registration" is sanctioned runtime
registration after that moment.

**Registration gate**:
The always-on wall that throws on any raw registry write outside a policy commit scope.
Not the seal - the gate is a wall, the seal is a moment.

**Override**:
The `{ override: true }` flag plus a dependency edge, letting exactly one pack legally
replace another's same-name registration. `kernel` and `engine` owners are non-overridable.

**Strict vs lenient validation**:
Per-pack mode: strict throws on unknown tags or properties, lenient downgrades them to
warnings. Missing required dependencies are fatal in both.

**Strict boot**:
Booting the engine against the published pack corpus with all gates armed; the pre-promote
gate for engine changes (`tests/tools/strict-boot-gate.js`).

**Jint**:
The single sandboxed JavaScript engine instance hosting all pack scripts (timeout,
recursion, and memory capped; no CLR bridge). Pack scripts are native ES modules.

**Registry (engine)**:
A sealed engine store of named registrations - commands, tags, properties, themes, slots,
oracle tables. Unqualified "registry" in this repo means one of these, NOT the pack
distribution service (`tapestry-registry`, a separate repo).

## Combat

**Combat pulse**:
One combat round, fired by `CombatPulse` every 20 ticks. Each combatant swings at its
primary target - the first entry in its combat list.

**Swell**:
An embedded boss beat inside an ordinary fight. Any in-combat entity with a non-empty
`swell_window` dial is a swell boss; the swell clock walks Baseline, Telegraph, Window,
Resolve phases and intercepts battle-pace commands while active.

**Telegraph**:
The swell phase that locks one boss attack line and emits a decelerating wind-up. How much
the wind-up reveals is governed by the `tell` dial.

**Window**:
The swell phase where the first committed counter verb triggers validation. A window that
times out with nothing committed is "weathered."

**Counter verb**:
A battle-pace command committed during a swell window to answer the telegraphed attack.

**Countered / Whiffed / Weathered**:
The three swell outcomes: right counter, wrong counter, no counter. Unrelated to the
weather system.

**Dial**:
A boss property lever driving swell timing and magnitude (`swell_window`,
`swell_chunk_pct`, `tell`, ...). Dials are content, not code.

**Pace**:
A command's combat-tempo axis: `free` commands always dispatch, `battle` commands are
intercepted by the swell clock during an active swell.

**Wimpy**:
The HP percentage at which an entity auto-flees each combat pulse. Players set it with the
`wimpy` command (0-50%).

**Flee cooldown**:
The post-flee window blocking both re-flee and re-engage, so nothing chain-flees.

**Disposition**:
An entity's reaction stance (Neutral, Friendly, Hostile), evaluated from alignment and tag
rules. ALL aggro comes from disposition.

**Behavior**:
A named movement plugin on a mob (`stationary`, `wander`, `patrol`). Orthogonal to
disposition - hostility is not a behavior.

**Posture gate**:
Resting and sleeping mobs skip behavior dispatch; sleeping mobs also skip disposition.

**Quarantine**:
A misbehaving behavior's fate: three budget overruns and it is silently skipped for the
server lifetime.

## Mobs and spawning

**Spawn rule**:
An inline room-YAML entry (`mob`, `count`, optional `rare`, `override`) defining what an
area reset places in that room.

**Persistent (spawn)**:
A spawn-rule flag reserving the slot across resets and preventing respawn once the mob is
killed. Nothing to do with disk persistence.

**Rare spawn**:
A per-slot probability of substituting an alternate template on reset.

**Spawn override / frozen spawn**:
A per-instance blob (name, desc, stats, items) applied verbatim on every spawn of that
slot, skipping the rare-swap. How oracle-authored mobs stay themselves.

## Character systems

**Train**:
A training point spent to raise a stat by 1, up to the race cap.

**Practice**:
Raising an ability's proficiency cap one tier (Novice 25, Apprentice 50, Journeyman 75,
Master 100) at a skill trainer. Distinct from trains.

**Bucket**:
A named alignment range (`evil`, `neutral`, `good`) synced to an `alignment_<bucket>` tag.

**Essence vs rarity**:
Two separate registered item-quality axes, both stored as string properties: essence
carries a glyph, rarity carries display order and decorators.

## Sessions and output

**Link-dead**:
A disconnected but retained session; the entity gains a `linkdead` tag and is reaped after
a timeout.

**Session takeover**:
A reconnecting player claiming their live or link-dead session via an interactive confirm.

**Wizlock**:
A runtime-only flag refusing non-admin logins. Not persisted; resets on reboot.

**GMCP**:
The JSON side-channel to clients (`Package.Name` payloads over telnet option 201 or
WebSocket envelopes). The engine's accessibility floor: every player-facing message is
mirrored as `Response.Feedback`.

**Output chain**:
The decorator pipeline every output string passes through: color rendering, tee (watch
mirroring), word wrap, then transport.

**Watch**:
Mirroring a player's rendered output to subscribed admin watchers via the tee decorator.
_Avoid_: snoop (legacy name)

**ASCII contract**:
All player-facing output is strict 7-bit ASCII. LLM-generated text passes through
`AsciiFold` before it reaches a player.

## Oracle and authoring

**Oracle**:
The solo-play generative authoring family: areas minted at runtime through play, grounded
by LLM recommend calls. An oracle area has no source pack, populates once, and accepts
stub exits.

**Oracle table**:
A frozen weighted-entry table keyed `<areaId>:<kind>` in a sealed engine registry.
Read-only to packs; rolling against it happens in pack JS. A different object from the
oracle pipeline itself.

**Recommend**:
The engine's async LLM suggestion seam (`authoring.recommend`). Structured output mode
returns schema-validated JSON.

**Consequence / stamp**:
An in-memory-only room overlay recording opaque `kind`/`lifespan` entries, evicted on area
ticks. Never persisted - reboots evaporate consequences.

**Seed**:
A persisted long on an area making its generated content replay as a pure function of the
seed across restarts and sharing.

**Harvest**:
Exporting live in-game edits into a portable, versioned pack. The workflow lives in the
CLI; the engine's side-cars and provenance labels are what make it possible.

## Flagged ambiguities

Words that legitimately mean different things in different systems. Qualify them.

- **heartbeat** - three meanings: the tick handler named "heartbeat"; the
  `HeartbeatManager` pulse dispatch it drives; and the telnet `IAC NOP` liveness probe.
  Say "pulse dispatch" or "connection heartbeat" when it matters.
- **pulse** - cadence-relative: 20 ticks for combat, 1 tick for the swell clock. Never say
  "pulse" to mean a fixed duration.
- **persistent** - the spawn-rule flag is a respawn cap, not disk persistence.
- **seal** - the registration seal and `HelpSeal` are different boot barriers in different
  subsystems.
- **registry** - an engine registry (sealed, in-process) vs the pack registry (the
  distribution service, separate repo). Unqualified means engine.
- **weather** - the world-simulation system vs the "weathered" swell outcome. Unrelated.
- **tell** - the swell reveal dial vs the player tell/whisper command.
- **oracle** - the authoring pipeline vs an oracle table. Related family, different
  objects.
- **role** - actor-type vs privilege. Only privilege roles gate dispatch.
- **track** - a progression track (XP container) vs the verb "to track."
- **source pack** - for pack content it is the pack namespace, but an authored oracle
  table's `SourcePack` holds the area id instead.
- **safe vs no_kill** - both mean "no combat" at different scopes: `safe` is a room tag
  (no engaging here), `no_kill` is an entity tag (this one is unattackable). Keep the
  scope in mind; they are not interchangeable.
- **terrain vs biome** - terrain is the weather/time exposure axis (indoors, outdoors,
  underground); biome is the ecological world layer (`biome:` field, desugars to a room
  tag). Content sometimes smuggles biome values into `terrain` - that is drift, not
  intent (vocabulary audit 2026-07-01).
- **fixture vs no_get** - fixture classifies permanent room furniture, no_get blocks
  pickup on anything. Today content double-tags both; the intended relationship is
  fixture implies no_get.
