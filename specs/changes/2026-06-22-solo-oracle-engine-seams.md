---
release: 0.1.41
specs: [heartbeat.md, mob-lifecycle.md, area-authoring.md, scripting-runtime.md, world-geography.md]
---

# Solo Oracle Engine Seams

## Why

Four engine seams needed for the solo-oracle slice-1 feature (generative area exploration):
frozen spawn override, pack recommend context, stub exits + lazy-mint movement, and repop-off
guard. These are the runtime hooks an oracle pack uses to mint rooms on demand, roll and freeze
mob rosters, and keep minted areas static while the player explores.

## What

### E1: Frozen spawn override (mob-lifecycle.md, heartbeat.md)

- SpawnManager accepts per-room override data (`SpawnOverride`) carrying a mob template id,
  display name, description, max-hp, damage, and item list. When the oracle mints a room it
  freezes the rolled roster into the room side-car; on reload SpawnManager reads the override
  and spawns the frozen mob instead of rolling fresh.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs; src/Tapestry.Engine/Authoring/RoomSpawnData.cs)

### E2: Pack recommend context (scripting-runtime.md, area-authoring.md)

- `tapestry.authoring.recommend(options, callback)` is callable headlessly (no interactive
  builder session). The engine projects room context via `RoomProjector` + `RoomPromptBuilder`.
  `PackRoomContext` wraps a projected `RoomData` plus pack-supplied template, system voice, and
  vars map. `LlmRecommendProvider` routes it to the pack-driven builder overload.
  Output passes through `OutputSanitizer.Clean` + `AsciiFold` for strict 7-bit ASCII.
  (src/Tapestry.Scripting/Modules/WorldAuthoringModule.cs; src/Tapestry.Authoring/LlmRecommendProvider.cs)

### E3: Stub exits + lazy-mint movement (world-geography.md, area-authoring.md)

- `Exit.IsStub` bool marks a placeholder exit with no real target room.
- `StubExitResolver` singleton accepts a JS-callable delegate; movement calls TryResolve when
  a player steps into a stub, the resolver mints the neighbor and wires the exit, then movement
  completes. No resolver degrades gracefully.
- `World.MoveEntity` (both overloads) accept optional `StubExitResolver?` parameter.
- `ApiWorld.MoveEntity` passes the injected resolver automatically.
- `ExitData` POCO + `ExitDataConverter` IYamlTypeConverter: non-stub exits emit bare scalars
  (byte-identical to the legacy string form); stub exits emit `{stub: true, label: "..."}`.
- `RoomData.Exits` changed from `Dictionary<string, string>` to `Dictionary<string, ExitData>`.
- `RoomProjector.Project` emits the correct ExitData variant.
- `YamlContentLoader.ParseExit` detects stub mappings and returns `Exit("") { IsStub=true }`.
- `AuthoredRoomLoader` skips the missing-target warning for stub exits.
- `WorldAuthoringModule` exposes `setStubExit` and `registerStubResolver` to pack JS.
  `setStubExit` is oracle-area locked (only runtime-authored areas without SourcePack).

### E4: Repop-off guard (mob-lifecycle.md, heartbeat.md)

- `AreaTickState` carries a `RepopDisabled` flag. When set for an area, `SpawnManager.Tick`
  skips all respawn logic for that area's rooms. The oracle pack sets this flag after minting
  rooms to prevent the standard repop loop from interfering with frozen rosters.
  (src/Tapestry.Engine/AreaTickState.cs; src/Tapestry.Engine/Heartbeat/...)
