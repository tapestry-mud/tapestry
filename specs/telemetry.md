---
capability: telemetry
last-updated: 2026-06-12
---

# Telemetry

## Overview

Telemetry is emitted via .NET `System.Diagnostics.Metrics` (meter `"Tapestry"`,
version `"1.0.0"`) and `System.Diagnostics.ActivitySource` (source `"Tapestry"`,
version `"1.0.0"`). Twenty-five named metrics cover tick throughput, connection
and session lifecycle, flood protection, mob AI, and world census. Bad-input
tracking accumulates unrecognized command verbs in memory and increments an OTel
counter per occurrence.

---

## Behavior

### Meter and tracing source

- Meter name: `"Tapestry"`, version `"1.0.0"`.
  [src/Tapestry.Engine/TapestryMetrics.cs L7]
- ActivitySource name: `"Tapestry"`, version `"1.0.0"`.
  [src/Tapestry.Engine/TapestryTracing.cs L7-9]

### Defined metrics

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

### BadInputTracker

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

---

## Rejected and Reverted

None.

---

## Change Log

| Change Record | Summary |
|---------------|---------|
