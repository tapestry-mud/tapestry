---
release: local-dev-2026-06-25
specs: [area-authoring.md, pack-loading.md]
---

# Solo Oracle V2 Completion

## Why

Three playtest gaps from the solo-oracle-v2 ship needed closing: minted loot was never
reaching the player (no item-template freeze seam), the LLM-off flow asked a question it
ignored instead of presenting baked-set choices, and the LLM table parser leaked preambles
and numbering into mob names and prose. A boot-crash bug was also found during playtest:
AuthoredRoomLoader was picking up item side-cars and oracle tables as rooms, causing a
PackValidator crash on the second boot.

## What

- `authoring.writeItemTemplate({ areaId, id, base, name, desc, type?, properties })` seam
  added to `WorldAuthoringModule`: clones a base `ItemTemplate` from `ItemRegistry`, overlays
  rolled properties (including a nested `ac` map coerced to `Dictionary<string,int>` for
  exact-type combat reads), registers into the live `ItemRegistry`, and writes a standalone
  `items/<id>.yaml` side-car that round-trips through `LoadItem`. (area-authoring.md)

- `AuthoredItemLoader` added: boot/reload scanner for `<authoring-root>/**/items/*.yaml`.
  Mirrors `AuthoredOracleLoader`. Loads each side-car via `LoadItem` with a real
  `PropertyRegistry` (so the nested `ac` map coerces correctly), maps to `ItemTemplate`,
  registers. Wired as DI singleton in `ServiceCollectionExtensions` and invoked in
  `ContentLoadingModule.Load()` after the oracle loader. (pack-loading.md)

- `AuthoredRoomLoader` scan hardened: scoped to files whose immediate parent directory is
  named `rooms`, and skips `*-oracle-table.yaml` files. Prevents item side-cars and oracle
  tables from being parsed as rooms. 4 regression tests added. (area-authoring.md)
