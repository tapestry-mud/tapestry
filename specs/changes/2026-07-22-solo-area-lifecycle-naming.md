---
release: 0.1.51
specs: [registries-and-seal.md, world-entity-store.md, area-authoring.md]
---

# Solo Area Lifecycle Naming

## Why

The area create path had no inverse. `AreaRegistry`, `OracleTableRegistry`, and
`ItemRegistry` were all append-only, so an authored area (rooms, oracle tables, minted
item templates, on-disk side-cars) could be created but never torn down. A caller that
wanted to discard a generated area had no engine primitive to do it, and no safe way to
remove rooms without stranding any player still standing in them.

## What

Three append-only registries gain a scoped removal path:

- `AreaRegistry.Unregister(id)` removes one area definition and returns whether it existed.
- `OracleTableRegistry.RemoveByArea(areaId)` removes every table whose id starts with
  `<areaId>:` (OrdinalIgnoreCase, matching its comparer).
- `ItemRegistry.RemoveByArea(areaId)` removes every template whose id starts with
  `<areaId>:` (Ordinal, because the backing dictionary is case-sensitive).

The trailing colon is load-bearing: it is the id separator, so a prefix match cannot reach
a sibling area whose slug string-prefixes the target. All three are idempotent and return a
count (or bool) rather than throwing on a miss. `RuntimeNamespaceStore` deliberately gains
no removal path - a discarded area's pack survives the discard, so its namespace
legitimately still exists.

`WorldAuthoringModule` gains the teardown surface that consumes them:

- `EvacuateArea(areaId, recallRoomId)` moves every player standing in the area's rooms to a
  recall room before those rooms are removed. It returns the number moved, `0` when the area
  is empty of players, and `-1` when a player is present but the recall room does not exist
  in the World - the signal for the caller to abort rather than strand anyone. Only players
  move; mobs and floor items are left for the caller to untrack. Each move publishes a
  `player.moved` event so GMCP room updates and quest watchers stay consistent. The recall
  room id is a caller parameter; `DefaultRecallRoomId` is a documented module-level fallback
  that mirrors `FlowEngine.DefaultSpawnRoomId`, so no engine assembly hardcodes pack content.
- `authoring.deleteArea(areaId)` is the atomic inverse of the create path, in one C# call
  (no partial-state window observable from JS). Order, and it matters: evacuate (abort whole
  on `-1`); `UntrackEntityDeep` every remaining entity; `World.RemoveRoom` per room;
  `ConsequenceOverlay.ClearRoom` per room; unregister from the three scoped registries; then
  one recursive delete of the on-disk area directory (`area.yaml`, `rooms/`, the frozen
  oracle tables, `items/`). Returns `false` when the area is unknown to the registry, the
  World, and the disk, or when evacuation failed; otherwise `true`. Idempotent-safe.

Deliberately never touched by the sweep: `RuntimeNamespaceStore` and its
`runtime-namespaces.txt` marker, the destination pack scaffold, and `server.yaml`. Deleting
an area does not delete the pack that holds it, so the pack's namespace legitimately still
exists and no boot-time haunting follows. Item templates under the area go with the
directory; items already instanced in a player's inventory are entities on the player file
and are not touched. `WorldAuthoringModule` takes `ConsequenceOverlay` and `EventBus` as two
new optional constructor dependencies to support the sweep and the move events.
