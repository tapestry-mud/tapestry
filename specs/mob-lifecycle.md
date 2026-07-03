---
capability: mob-lifecycle
last-updated: 2026-06-22
---

# Mob Lifecycle

## Overview

Mob lifecycle covers everything from template definition through entity instantiation,
area reset spawning, stat derivation, and loot assignment. Key engine classes:
`MobTemplate` (template model), `SpawnManager` (area reset, per-room repopulation,
persistent cap, rare spawn), `MobStatDerivation` (class/race stat application), and
`LootTable` / `LootTableResolver` (loot definition and weighted-random resolution).
Behavior dispatch and disposition evaluation are out of scope here (see mob-ai.md).

## Behavior

### MobTemplate model

- A `MobTemplate` is the static blueprint for a mob type. It is loaded from YAML by
  `YamlContentLoader.LoadMob` and registered with `SpawnManager.RegisterTemplate`.
  (src/Tapestry.Scripting/YamlContentLoader.cs:124;
  src/Tapestry.Engine/Mobs/SpawnManager.cs:54-57)
- Key template fields and their YAML keys:
  - `Id` / `id` -- globally unique template identifier (required).
  - `Name` / `name` -- display name applied to spawned entities.
  - `Type` / `type` -- entity type string, defaults to "npc".
  - `Tags` / `tags` -- list of entity tags copied to each spawned entity.
  - `Keywords` / `keywords` -- target keywords for player commands.
  - `BaseDisposition` / `base_disposition` -- Neutral, Friendly, or Hostile; sets
    `entity.Disposition` at spawn. (src/Tapestry.Engine/Mobs/MobTemplate.cs:53)
  - `Behavior` / `behavior` -- named behavior to assign; default "stationary".
    (src/Tapestry.Engine/Mobs/MobTemplate.cs:54)
  - `Stats` / `stats` -- base stats block (strength, intelligence, wisdom, dexterity,
    constitution, luck, max_hp, max_resource, max_movement).
    (src/Tapestry.Engine/Mobs/MobTemplate.cs:55)
  - `Properties` / `properties` -- arbitrary key/value pairs written to entity properties
    at spawn (e.g., mob_level, wimpy_pct, wander_interval).
  - `Equipment` / `equipment` -- list of item template IDs to instantiate and equip on
    spawn; stat modifiers on equipped items are applied to entity stats.
    (src/Tapestry.Engine/Mobs/SpawnManager.cs:135-161)
  - `LootTable` / `loot` (inline) -- populated by the YAML loader from the inline `loot:`
    block; the table is auto-keyed to the template ID.
    (src/Tapestry.Scripting/YamlContentLoader.cs:190-201)
  - `Class` / `class` and `Race` / `race` -- optional class and race IDs for stat
    derivation; see MobStatDerivation below.
  - `Level` / `level` -- used with class for stat growth scaling.
  - `Disposition` / `disposition` -- optional rules block (default + rules list) for
    alignment-based disposition overrides.
    (src/Tapestry.Engine/Mobs/MobTemplate.cs:62)
  - `IdleCommands` / `idle_commands`, `IdleChance` / `idle_chance`,
    `IdleInterval` / `idle_interval` -- idle command configuration.
  - `BattleCommands` / `battle_commands`, `BattleChance` / `battle_chance`,
    `BattleInterval` / `battle_interval` -- combat command configuration.
  - `Abilities` / `abilities` -- list of ability entries (id, optional proficiency).
  - `PatrolRoute` / `patrol_route` -- ordered room-ID list for patrol behavior.
  - `Script` / `script` -- optional script file path for per-template hooks.
  (src/Tapestry.Engine/Mobs/MobTemplate.cs:46-174)
- `MobTemplate.CreateEntity()` instantiates a new `Entity`, copies tags and keywords,
  sets base stats, writes all properties from the `Properties` dict, stamps `template_id`,
  `behavior`, and optional patrol_route / idle / battle / disposition data, then returns
  the entity before it has been placed in the world.
  (src/Tapestry.Engine/Mobs/MobTemplate.cs:77-173)

### SpawnManager and area reset spawning

- `SpawnManager` subscribes to `area.tick` on construction; each event triggers
  `RunAreaReset(areaId)` and `RestorePlacements(areaId)`.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:41-52)
- `AreaTickService.Tick()` fires `area.tick` per area once `reset_interval` engine ticks
  have elapsed. When players are present in an area the effective interval is multiplied
  by `occupied_modifier` (default 3.0), slowing resets in populated areas.
  (src/Tapestry.Engine/AreaTickService.cs:26-57;
  packs/@tapestry/example-pack/areas/starter-town/area.yaml -- occupied_modifier: 3.0)
- Spawn rules are authored inline in room YAML under the `spawns:` key, each entry with
  `mob:` (template ID), `count:`, optional `rare:` block, and optional `tags:` list.
  `PackLoader` reads `RoomLoadResult.Spawns` and calls
  `SpawnManager.RegisterRoomSpawns` to index each rule.
  (src/Tapestry.Scripting/YamlContentLoader.cs:20-26;
  src/Tapestry.Engine/Mobs/SpawnManager.cs:79-102;
  packs/@tapestry/example-pack/areas/starter-town/rooms/wilderness-path.yaml:14-19)
- On `RunAreaReset`: dead mob IDs are first pruned from the spawn-tracking dict; then
  for each `SpawnRule`, living count is computed from tracking entries. If the rule has
  the "persistent" tag, it is skipped when living count is already at `Count` -- surviving
  persistent mobs carry their spawn-slot reservation regardless of their current room.
  Non-persistent rules repopulate to `Count` on every reset.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:218-270;
  src/Tapestry.Engine/Mobs/SpawnRule.cs:9-17)
- `SpawnMob(templateId, roomId, over?)` is the core spawn path: calls `template.CreateEntity()`,
  applies `MobStatDerivation.Apply`, then applies a `SpawnOverride` blob if present (see below),
  places the entity in the room, instantiates and equips equipment items, resolves and places loot,
  registers proficiencies for abilities, then publishes `mob.spawn`.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:115-215)

### Frozen spawn override

- `SpawnOverride` is a sealed class carrying per-instance facts captured at generation time:
  `FromType` (source template hint), `Name`, `Desc`, `MaxHp`, `Damage` (dice expression),
  `Items` (item template IDs to add to contents), and `NoReroll` (bool).
  (src/Tapestry.Engine/Mobs/SpawnOverride.cs)
- `SpawnRule.Override` holds an optional `SpawnOverride?`; when set, `RunAreaReset` applies it
  on every spawn of that slot and skips the rare-swap (the frozen instance always returns).
  (src/Tapestry.Engine/Mobs/SpawnRule.cs:17; src/Tapestry.Engine/Mobs/SpawnManager.cs)
- `SpawnManager.ApplyOverride` is called inside `SpawnMob` immediately after
  `MobStatDerivation.Apply`. It writes `Name`, `description`, `BaseMaxHp`/`Hp`,
  `damage_dice`, `oracle_from_type`, and contents items from the blob - verbatim, no
  re-roll. `Hp` is clamped to the new `MaxHp` after `BaseMaxHp` is set.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs)
- `RegisterRoomSpawns` tuple gains an `Override: SpawnOverride?` member; callers that have
  no frozen override pass `null`. The `PackLoader` authored-room path passes `null`.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:79-102;
  src/Tapestry.Scripting/PackLoader.cs:325-329)
- JS: `mobs.spawnMob(options)` where `options` is `{ template, roomId, override?: { fromType,
  name, desc, maxHp, damage, items, noReroll } }`. The binding accepts a single `JsValue`
  options object and unpacks manually per the Jint convention.
  (src/Tapestry.Scripting/Services/ApiMobs.cs; src/Tapestry.Scripting/Modules/MobsModule.cs)
- Spawn-tracking dict maps spawned entity `Guid` to `(areaId, spawnIndex)`. This allows
  persistent-tag cap checks to count survivors across rooms.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:25, 247-268)

### Rare spawn probability

- Each `SpawnRule` supports a `Rare` block (`RareSpawnConfig`) with `Mob` (alternate
  template ID) and `Chance` (0.0-1.0 probability).
  (src/Tapestry.Engine/Mobs/SpawnRule.cs:1-7)
- During `RunAreaReset`, for each missing spawn slot, if `rule.Rare != null` and
  `_random.NextDouble() < rule.Rare.Chance`, the rare template ID is used in place of
  the normal one for that slot. Rare and normal both consume one tracking slot.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:257-261)

### MobStatDerivation

- `MobStatDerivation.Apply(entity, template, classes, races)` is called immediately after
  `CreateEntity()` inside `SpawnMob`, before the entity is placed in the world.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:129)
- If `template.Class` is set and `template.Level > 0`, the class definition's
  `StatGrowth` dict is iterated; each value is a dice-notation string (e.g., "1d4").
  `AverageDice` converts dice notation to an integer average -- for NdX+M the formula
  is `N * (X + 1) / 2 + M` (integer math). The result times `template.Level` is added
  to the entity's base stat. After all stat growth is applied, current vitals are
  restored to the new maximums (Hp = MaxHp, Resource = MaxResource, Movement = MaxMovement).
  The `class` and `level` properties are written to the entity.
  (src/Tapestry.Engine/Mobs/MobStatDerivation.cs:17-37)
- If `template.Race` is set, racial flags from `RaceDefinition.RacialFlags` are added
  as entity tags and the `race` property is written to the entity.
  (src/Tapestry.Engine/Mobs/MobStatDerivation.cs:39-50)
- If neither class nor race is set, stat derivation is a no-op; the entity keeps the
  base stats copied from the template's `Stats` block.

### LootTable and LootTableResolver

- Loot is defined inline in a mob YAML file under the `loot:` key. The loader constructs
  a `LootTable` keyed to the template ID and calls `SpawnManager.RegisterLootTable`.
  (src/Tapestry.Scripting/YamlContentLoader.cs:190-201;
  src/Tapestry.Scripting/PackLoader.cs:476-480)
- `LootTable` fields:
  - `Guaranteed` -- list of `{ item, count }` entries; every item is always included.
  - `Pool` -- list of weighted entries `{ item, weight }`.
  - `PoolRolls` -- number of independent draws from the pool.
  - `RareBonus` -- optional `{ chance, pool }` block; a single weighted-random draw
    if the chance roll succeeds.
  (src/Tapestry.Engine/Mobs/LootTable.cs)
- `LootTableResolver.Resolve(table)` executes the resolution:
  1. All guaranteed items are added (Count times each).
  2. `PoolRolls` independent weighted-random draws are made from `Pool`.
  3. If `RareBonus` is set and `_random.NextDouble() < RareBonus.Chance`, one
     weighted-random draw is made from `RareBonus.Pool`.
  Returns a `List<string>` of item template IDs.
  (src/Tapestry.Engine/Mobs/LootTableResolver.cs:13-47)
- Weighted-random selection: total weight is summed; a random integer in [0, totalWeight)
  is drawn; entries are scanned cumulatively until the roll is covered.
  (src/Tapestry.Engine/Mobs/LootTableResolver.cs:49-70)
- At spawn, if the template references a loot table, `SpawnMob` resolves it, creates
  each item via `ItemRegistry.CreateItem`, places items in the entity's contents, tracks
  them in the world, then publishes `mob.loot.generated` with `loot_count`.
  (src/Tapestry.Engine/Mobs/SpawnManager.cs:163-188)
- Example (goblin.yaml): guaranteed goblin-ear, 1 pool draw from rusty-dagger/leather-
  scrap/small-coin-pouch (weights 50/35/15), 5% chance for goblin-charm or emerald-shard.
  (packs/@tapestry/example-pack/areas/starter-town/mobs/goblin.yaml:37-55)

## Rejected and Reverted

- None on record.

## Change Log

- 2026-06-22 [solo-oracle-e1-frozen-spawn-override](changes/2026-06-22-solo-oracle-e1-frozen-spawn-override.md)
