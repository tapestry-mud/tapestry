---
capability: admin-commands
last-updated: 2026-06-12
---

# Admin Commands

## Overview

Admin commands are pack-registered JavaScript handlers gated by a privilege
check in the command router. The engine contributes one kernel admin command
(`badinput`). The JS scripting surface is `tapestry.admin` and
`tapestry.watch`. The wizlock flag lives in the engine but is toggled through
a pack command.

---

## Behavior

### Admin privilege gate

- All pack admin commands carry `admin: true` in their registration; the
  command router enforces role-gating before dispatch.
  (packs/@tapestry/core/scripts/commands/admin-teleport.js)
- The kernel `badinput` command is registered with `roles: ["admin"]`.
  (src/Tapestry.Server/Modules/BadInputModule.cs)
- Wizlock is a runtime-only flag on `WizlockState`. While set, the login flow
  refuses non-admin characters; already-connected players are unaffected.
  The flag is not persisted and resets to unlocked on reboot (ROM parity).
  (src/Tapestry.Engine/Login/WizlockState.cs; commit be2610e)
- JS surface: `tapestry.admin.setWizlock(bool)` / `tapestry.admin.isWizlocked()`.
  The pack `wizlock` command toggles the state.
  (packs/@tapestry/core/scripts/commands/admin-wizlock.js)

### Entity management: set

- `set` dispatches through `AdminModule.DispatchSet`. Settable targets are
  `player`, `npc`, `item`, and `room`.
  (src/Tapestry.Scripting/Modules/AdminModule.cs:193-289)
- For `player`, `npc`, and `item` the engine resolves the target from the
  calling admin's context: players by online session, NPCs from the admin's
  current room, items from the admin's inventory/equipment.
  (src/Tapestry.Scripting/Modules/AdminModule.cs:541-689)
- Ordinal syntax (`2.keyword`) is supported for disambiguating multiple matches.
  (src/Tapestry.Scripting/Modules/AdminModule.cs:553-558)
- For `room`, no target token is accepted; the admin's current room is the
  implicit target. (src/Tapestry.Scripting/Modules/AdminModule.cs:235-259)
- Declared attributes (PropertyRegistry / TagRegistry entries marked
  `IsAdminSettable`) flow through `AttributeWriter`. Out-of-registry
  subsystem ops (stats, alignment, gold, npc hp, proficiency, training cap)
  are handled by retained pack-side domain handlers in `admin-set.js`.
  (packs/@tapestry/core/scripts/commands/admin-set.js:52-114)
- `set ?`, `set [kind] ?`, and `set [kind] [attr] ?` display discovery panels
  rendered via `PanelRenderer`. (src/Tapestry.Scripting/Modules/AdminModule.cs:196-231)

### Entity management: grant

- `grant` dispatches through `AdminModule.DispatchGrant` to JS handlers
  registered via `tapestry.admin.grant.register({kind, type, handler, ...})`.
  Allowed kinds: `player`, `npc`, `item`, `room`.
  (src/Tapestry.Scripting/Modules/AdminModule.cs:291-350)
- Duplicate `(kind, type)` keys log a warning; the later registration wins.
  (src/Tapestry.Scripting/Modules/AdminModule.cs:186-190)
- Built-in grant types in core: `player xp`, `player train`, `player gold`.
  (packs/@tapestry/core/scripts/commands/admin-grant.js)
- `grantrole` / `revokerole` add or remove roles from online players via
  `tapestry.world.addRole`. Separate commands from `grant` to avoid keyword
  collision. (packs/@tapestry/core/scripts/commands/admin-grant-role.js)

### World manipulation: spawn, loaditem, purge, restore, peace

- `spawn <templateId>` spawns a mob from a template into the admin's current
  room. (packs/@tapestry/core/scripts/commands/admin-spawn.js)
- `loaditem <templateId>` instantiates an item template into the admin's
  inventory. (packs/@tapestry/core/scripts/commands/admin-loaditem.js)
- `purge [npc|items|all]` removes matching entities from the admin's room;
  defaults to all. (packs/@tapestry/core/scripts/commands/admin-purge.js)
- `restore [player|all]` refills vitals for a named player or all online
  players via `tapestry.stats.restoreVitals`.
  (packs/@tapestry/core/scripts/commands/admin-restore.js)
- `peace` removes all combatants in the admin's room from combat.
  (packs/@tapestry/core/scripts/commands/admin-peace.js)

### Teleport / goto

- `teleport <player> <roomId>` (aliases: `tp`) moves an online player to a
  room by id. Fails if the player is not online or the room id is unknown.
  (packs/@tapestry/core/scripts/commands/admin-teleport.js)

### Inspect and locate

- `inspect [entity]` shows full stats, properties, tags, equipment, inventory,
  alignment, proficiency for a visible entity in the admin's room.
  (packs/@tapestry/core/scripts/commands/admin-inspect.js)
- `inspect room [id]` shows name, description, area, biome, terrain, flags,
  properties, exits, and occupants for the given room (defaults to current).
  (packs/@tapestry/core/scripts/commands/admin-inspect.js:139-210)
- `inspect area [id]` shows area metadata including level range, reset interval,
  and provenance (pack-supplied, pack+edits, or hand-authored).
  (packs/@tapestry/core/scripts/commands/admin-inspect.js:212-234)
- `whereis <keyword>` scans all world entities; `mwhere` filters to NPCs;
  `owhere` filters to items and containers. Results cap at 100.
  (packs/@tapestry/core/scripts/commands/admin-whereis.js)

### Combat admin

- `peace` clears combat for all occupants in the admin's room (see above).
- `set npc hp <target> <value>` calls `admin.setEntityHp` which sets both
  base max HP and current HP simultaneously. (src/Tapestry.Scripting/Modules/AdminModule.cs:94-102)

### executeAs seam

- `tapestry.admin.executeAs(entityId, commandLine)` dispatches a command line
  as the target entity through the standard parse+route path. Privilege is NOT
  escalated; output goes to the target's session. Session-backed players only;
  returns `false` for sessionless or invalid entities.
  (src/Tapestry.Scripting/Modules/AdminModule.cs:111-133)
- This seam is the backing primitive for pack `force`/`at` commands.
  Full documentation is in command-dispatch.md.

### Watch / snoop

- `TeeConnection` is an output decorator inserted in the connection chain
  between `ColorRenderingConnection` and `WrappingConnection`. Every write is
  forwarded to the owning player and mirrored to all subscribed watchers.
  (src/Tapestry.Engine/Watch/TeeConnection.cs)
- `ShouldBroadcast` (default `true`) is a per-connection gate; a future
  producer may set it false for private writes. `WatchBroadcastScope.Suppressed`
  is a per-write suppression seam. (src/Tapestry.Engine/Watch/TeeConnection.cs:23,49)
- Watcher sinks resolve live on each broadcast; a reconnected watcher (new
  connection, same entity id) keeps receiving output without re-subscribing.
  (src/Tapestry.Scripting/Modules/WatchModule.cs:43-51; commit 70db6ab)
- Self-subscription is rejected to prevent infinite recursion through the tee.
  (src/Tapestry.Scripting/Modules/WatchModule.cs:39)
- Admins may not snoop other admins; the `snoop` command checks the target's
  roles before calling `tapestry.watch.start`.
  (packs/@tapestry/core/scripts/commands/snoop.js:40-46)
- JS surface: `tapestry.watch.start(watcherEntityId, targetEntityId)` and
  `tapestry.watch.stop(watcherEntityId)`.
  (src/Tapestry.Scripting/Modules/WatchModule.cs:29-58)
- Full output pipeline ownership of TeeConnection is documented in
  output-pipeline.md.

---

## Rejected and Reverted

- **admin.restoreVitals JS seam** -- added in commit c0696c7, reverted in
  e1697f0 (`revert: drop duplicate admin.restoreVitals seam`). Packs must use
  `tapestry.stats.restoreVitals` directly.

---

## Change Log

---

## Related capabilities

- command-dispatch.md: executeAs seam, `admin-at.js`, `admin-force.js`.
- output-pipeline.md: full TeeConnection chain (ColorRendering -> Tee -> Wrapping).
- sessions-and-connections.md: session lifecycle, link-dead timers, despawn logic.
- login-and-accounts.md: WizlockGate enforcement during login.
