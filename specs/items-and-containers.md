---
capability: items-and-containers
last-updated: 2026-06-12
---

# Items and Containers

## Overview

Covers the item template model, runtime instantiation, player inventory
(pickup, drop, give, destroy), keyword resolution, stacking display,
containers, consumables, and loot table resolution. Persistence of
inventory state is covered in persistence.md. Mob death loot-drop timing
is covered in mob-ai.md. Economy, pricing, and shop transactions are
covered in economy-and-shops.md. Equipment slots and stat modifiers are
covered in equipment-and-modifiers.md.

## Behavior

### Item template model

- Templates are defined in YAML with fields `id`, `name`, `type`, `tags`,
  `keywords`, `properties`, `modifiers`, `spawn_on`, and `contents`.
  (src/Tapestry.Engine/Items/ItemTemplate.cs:8-17)

- `modifiers` is a list of `{ stat, value }` entries; valid stats are members
  of the `StatType` enum. At instantiation, all modifiers are stored on the
  entity under the `modifiers` property (transient, not persisted).
  (src/Tapestry.Engine/Items/ItemTemplate.cs:44-52)

- Instantiation calls `CreateEntity()`: copies tags, keywords, and properties
  onto a new entity; sets `template_id` to the template id; skips a property
  named `room_id` if present. Nested `Dictionary<object, object>` property
  values are recursively normalized to `Dictionary<string, object?>`.
  (src/Tapestry.Engine/Items/ItemTemplate.cs:19-55)

- `ItemRegistry` stores templates by id and exposes `CreateItem(templateId)`,
  `GetTemplate`, `HasTemplate`, and `AllTemplates`.
  (src/Tapestry.Engine/Items/ItemRegistry.cs:7-33)

- Scripts create items via `items.createFromTemplate(templateId)` (world-tracked,
  not yet in any inventory) or `items.spawnToInventory(templateId, entityId)` /
  `items.spawnToContainer(templateId, entityId)` which add the new item directly
  to the target entity's contents.
  (src/Tapestry.Scripting/Modules/ItemsModule.cs:24-114)

### Inventory -- pickup, drop, give, destroy

- `InventoryManager.PickUp` blocks items tagged `fixture` or `no_get`. If the
  entity has `max_carry_weight > 0` the combined weight of current contents plus
  the item must not exceed that limit.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:25-69)

- After pickup, `CurrencyService.TryAutoConvert` is called; if it returns true
  (the item was auto-converted to currency), no `entity.item.picked_up` event
  is published. Otherwise the event fires unless `silent: true`.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:47-67)

- `PickUp` removes the item from its room before adding it to the entity's
  contents. `Drop` is the reverse: removes from contents, adds to the current
  room. Both fire bus events unless called silently.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:40-99)

- `Give(from, to, item)` moves an item between two entities without touching
  any room; fires `entity.item.given`. Auto-convert is attempted on the receiver.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:102-134)

- `Destroy(holder, item)` removes the item from the holder's contents and
  untracks it from the world. Use this for non-consumable item destruction
  (crafting materials, schematics). Does not fire an event.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:265-276)

- Weight of an entity's carried load is the sum of `weight` properties across
  all items in `Contents`; computed by `GetCarryWeight`.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:278-281)

- Engine-property constants: `slot`, `weight` (Double), `max_carry_weight`
  (Double, Player/Npc only), `modifiers` (transient, no persistence).
  (src/Tapestry.Engine/Inventory/InventoryProperties.cs:8-18)

### Keyword resolution

- `KeywordMatcher.FindByKeyword` resolves a keyword against a collection using
  a three-pass priority: exact keyword match, then keyword prefix match, then
  case-insensitive name substring.
  (src/Tapestry.Engine/Inventory/KeywordMatcher.cs:26-28)

- Ordinal syntax `N.keyword` (e.g., `2.sword`) selects the Nth match (1-based)
  from the full set of matching entities. A bad ordinal returns null.
  (src/Tapestry.Engine/Inventory/KeywordMatcher.cs:12-23)

- `FindAllByKeyword` supports the keyword `all` (returns everything), `all.X`
  (returns all matching X), or a bare keyword (returns all matching).
  (src/Tapestry.Engine/Inventory/KeywordMatcher.cs:31-53)

- `getAll` (script) blocks items tagged `no_get` even when doing bulk pickup.
  (src/Tapestry.Scripting/Modules/InventoryModule.cs:333)

- Keyword lookup searches inventory, then equipped slots, then the room floor
  when called from `examineItem`; `pickUp` and `findInRoom` search only the
  room floor.
  (src/Tapestry.Scripting/Modules/InventoryModule.cs:197-240, 37-64)

### Containers

- Container entities have type `container`. Engine-registered properties:
  `public` (Bool), `container_capacity` (Int, max item count),
  `container_weight_limit` (Int, max total weight), `fill_type` (String),
  `fill_source` (String), `fill_supply` (Int).
  (src/Tapestry.Engine/Containers/ContainerProperties.cs:7-23)

- `PutInContainer` refuses if the container is not accessible (must be in the
  actor's inventory, in the same room floor, or flagged `public`).
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:136-168)

- Capacity and weight limit are checked before inserting; fail reasons are
  `full` and `too_heavy` respectively. A `container.item_adding` event is
  published and may cancel the operation.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:152-203)

- `getFromContainer` and `getAllFromContainer` publish a `container.access.check`
  event (action: "get") before extracting; scripts may cancel access.
  `putAllInContainer` publishes the same event (action: "put").
  (src/Tapestry.Scripting/Modules/InventoryModule.cs:525-614, 690-721)

- Container contents in `putAllInContainer` stop on the first `full` or
  `too_heavy` failure; other per-item failures (e.g., cancelled) are skipped
  silently without stopping. The loop iterates only non-container items from
  the actor's `Contents`.
  (src/Tapestry.Scripting/Modules/InventoryModule.cs:704-721)

- `FillItem` fills a liquid-bearing item (must have `max_charges`) from a source
  entity. The source entity's `fill_source` property determines the liquid type;
  if the property is absent or empty, the engine falls back to checking whether
  the source entity has a `fill_source` tag, defaulting to "water" when that
  tag is present. Refuses if liquid types would be mixed with existing charges.
  Decrements `fill_supply` on the source if set.
  (src/Tapestry.Engine/Inventory/InventoryManager.cs:205-263, especially 214-219)

### Stacking

- `StackingService.GetStacks` groups an entity's inventory into `StackEntry`
  records. Items without a `template_id` each form their own singleton stack.
  Items with a `template_id` are grouped by `templateId|essence|[extraKeys...]`.
  (src/Tapestry.Engine/Inventory/StackingService.cs:17-63)

- Extra stack-differentiation keys are registered at runtime via
  `stacking.addKey(propertyName)`. Packs use this to prevent items with
  different game-meaningful properties from collapsing into one visual stack.
  (src/Tapestry.Engine/Inventory/StackingService.cs:9-15)

- Each `StackEntry` exposes `templateId`, `name`, `quantity`, `rarityKey`,
  `essenceKey`, and `itemIds` (list of actual entity GUIDs in the stack).
  (src/Tapestry.Engine/Inventory/StackEntry.cs:3-11)

### Consumables

- Engine-registered item properties: `consume_method` (String), `sustenance_value`
  (Int), `effect_id` (String), `effect_duration` (Int, ticks), `effect_data`
  (MapInt, transient), `charges` (Int), `max_charges` (Int), `destroy_on_empty`
  (Bool).
  (src/Tapestry.Engine/Consumables/ConsumableProperties.cs:8-27)

- `ConsumableService.Consume(entityId, itemId)` requires the item to be in the
  entity's inventory. Items with `charges` must have `charges > 0`.
  (src/Tapestry.Engine/Consumables/ConsumableService.cs:31-45)

- A cancellable `item.consuming` event fires before any state mutation; if
  cancelled, `ConsumeReason.Cancelled` is returned.
  (src/Tapestry.Engine/Consumables/ConsumableService.cs:47-64)

- Each consume decrements `charges` by 1. If charges reach 0, the item is
  destroyed unless `destroy_on_empty` is explicitly false. Items without a
  `charges` property at all are always destroyed on consume.
  (src/Tapestry.Engine/Consumables/ConsumableService.cs:72-88)

- `item.consumed` is published while the item still exists (before destroy),
  carrying all result fields. Sustenance application is left to the survival
  pack; the engine does not apply it.
  (src/Tapestry.Engine/Consumables/ConsumableService.cs:94-110)

- `ConsumableResult` record fields: `Success`, `Reason` (enum), `ItemId`,
  `ItemName`, `ConsumeMethod`, `SustenanceValue`, `EffectId`, `EffectDuration`,
  `EffectData`.
  (src/Tapestry.Engine/Consumables/ConsumableResult.cs:12-22)

### Loot tables

- `LootTable` has an `Id`, a `Guaranteed` list (`{item, count}`), a `Pool`
  list (`{item, weight}`), `PoolRolls` (number of pool draws), and an optional
  `RareBonus` (`{chance, pool}`).
  (src/Tapestry.Engine/Mobs/LootTable.cs:4-29)

- `LootTableResolver.Resolve` adds all guaranteed items (respecting `count`),
  then performs `PoolRolls` weighted-random draws from the main pool, then
  makes one rare-bonus draw if a random double falls below `RareBonus.Chance`.
  (src/Tapestry.Engine/Mobs/LootTableResolver.cs:13-47)

- Pool selection uses cumulative-weight random; the last entry is returned as
  a fallback if floating-point rounding exhausts the loop without a match.
  (src/Tapestry.Engine/Mobs/LootTableResolver.cs:49-69)

- The resolver accepts an injected `Random` for deterministic test use.
  (src/Tapestry.Engine/Mobs/LootTableResolver.cs:8-10)

- Loot table definitions in mob YAML (see tests/fixtures/packs/example-pack/
  areas/starter-town/mobs/goblin.yaml) use top-level keys `guaranteed`, `pool`,
  `pool_rolls`, and optionally `rare_bonus`. The resolver returns a list of
  template id strings; instantiation and placement are handled by caller code
  (out of scope here -- see mob-ai.md).

## Rejected and Reverted

(No tombstones recorded at time of writing.)

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
