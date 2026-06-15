---
release: v0.1.36
specs: [area-authoring.md]
---

# Extend Baked In Areas

## Why

A builder could edit a packed area's prose but not grow it. The `dig` command
refused to carve a new room off a pack-owned room because a directional exit
written into a pack room's side-car vanishes when that room reloads from its pack
file on the next boot - so the way back would silently disappear. The refusal was
a guard against that vanishing exit, not a statement that extending a pack area is
wrong.

The engine already grew a connection system (records that re-apply an exit to both
endpoint rooms at runtime and persist under `data/connections/`, never writing into
either room's data) for the `link` command. Routing a dig boundary through a
connection removes the vanishing-exit reason entirely. This change adds the two
engine-side pieces that support that: a queryable home for connection records whose
anchor is gone, and a builder-facing flag that surfaces the resulting orphan.

## What

- `ConnectionLoader` now retains records it could not apply. `Load()` adds a record
  to `Loaded` only when both endpoint rooms exist; previously a record whose endpoint
  was missing was applied as a no-op and silently dropped. Those records now collect
  in a new `Dangling` list, giving the absent-anchor case a queryable home.
  (src/Tapestry.Scripting/Connections/ConnectionLoader.cs:Dangling)

- `GetAreaRooms` flags orphaned extensions. An authored room (no `source_pack`)
  referenced by a dangling connection record - the case where a later pack update
  removed the anchor room the extension hung off - gets `(orphaned)` appended to its
  provenance string (e.g. `[authored] (orphaned)`). Pack rooms are never flagged
  (the check is gated on `source_pack == null`). `rooms <area>` renders the provenance
  verbatim, so the orphan is visible to builders, not just in the boot-log warning.
  Detection is presence-based (any dangling record naming the room), not reachability
  analysis. (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs:GetAreaRooms)

- Test tooling: the telnet scenario runner gains a `Server: restart` step that bounces
  the managed server against the same save store and reconnects all players, enabling
  restart-survival assertions for connection-backed exits. Non-managed runs that hit
  the step fail with a clear message. (tests/tools/telnet-runner.js)

The `dig` command behavior that consumes these pieces lives in the builder pack and is
recorded with that pack's release.
