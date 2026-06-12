---
capability: equipment-and-modifiers
last-updated: 2026-06-12
---

# Equipment and Modifiers

## Overview

Covers equipment slots (SlotDefinition and SlotRegistry), equip and unequip
rules, stat modifiers applied by equipped items, essence, and rarity tiers.
Item template definition, inventory management, containers, consumables, and
loot tables are covered in items-and-containers.md. Persistence of inventory
state is covered in persistence.md.

## Behavior

### Equipment slots

- `SlotDefinition` has fields `Name`, `Display`, `Max` (maximum concurrent items
  in this slot), and `Scope` (`engine` or pack name). `IsEngineSlot` is true
  when Scope == "engine".
  (src/Tapestry.Engine/Inventory/SlotDefinition.cs:3-18)

- Slot names must match `^[a-z][a-z0-9_]*$` (snake_case); hyphens are rejected
  at registration time.
  (src/Tapestry.Engine/Inventory/SlotRegistry.cs:58-67)

- Multi-slot slots (Max > 1) use sub-keys `slotname:0`, `slotname:1`, ... up to
  `Max - 1` for individual equipment positions.
  (src/Tapestry.Engine/Inventory/EquipmentManager.cs:62-75)

- `EquipmentManager.Equip` requires the item to be in the entity's inventory;
  validates the slot exists; if all sub-slots are full, auto-swaps the first
  occupied sub-slot (sub-slot :0 for multi-slots) back to inventory.
  (src/Tapestry.Engine/Inventory/EquipmentManager.cs:32-103)

- Scripts register new slots via `equipment.registerSlot({name, display, max,
  override})` which records a `RegistrationCandidate` of kind "slot"; the actual
  `SlotRegistry` write is deferred until Resolve() (the seal barrier). Duplicate
  slot names from different packs without `override: true` are a boot error.
  (src/Tapestry.Scripting/Modules/EquipmentModule.cs:238-269)

- `getSlots` returns all slots in registration order with per-slot equipped item
  details including `rarityKey` and `essenceKey`.
  (src/Tapestry.Scripting/Modules/EquipmentModule.cs:181-232)

### Equip and unequip rules

- On equip, stat modifiers from the item's `modifiers` property are applied to
  the entity's stat block under the source key `equipment:<itemId>`. On unequip,
  those modifiers are removed by source key.
  (src/Tapestry.Engine/Inventory/EquipmentManager.cs:80-89, 115-119)

- Equip fires `entity.equipped`; unequip fires `entity.unequipped` (unless
  `silent: true`). The event carries `itemId` and the base slot name.
  (src/Tapestry.Engine/Inventory/EquipmentManager.cs:91-101, 120-136)

### Stat modifiers

- Item stat modifiers are stored on the entity under the `modifiers` property,
  which is registered as transient (no persistence flag set to false).
  (src/Tapestry.Engine/Inventory/InventoryProperties.cs:18)

- Because `modifiers` is transient, stat modifier data is lost across save/load.
  The persistence layer does not rehydrate `modifiers` from `template_id` on
  load. Any pack that needs modifiers restored after a server restart must
  re-equip the item or apply modifiers explicitly via a post-load hook.

- Engine-property constants: `slot`, `weight` (Double), `max_carry_weight`
  (Double, Player/Npc only), `modifiers` (transient, no persistence).
  (src/Tapestry.Engine/Inventory/InventoryProperties.cs:8-18)

### Essence

- `EssenceDefinition` is a record with `Key`, `Glyph`, and `Color`.
  (src/Tapestry.Engine/Inventory/EssenceDefinition.cs:3)

- Packs register essence types via `essence.register({key, glyph, color, html,
  override})`; registration is deferred to seal. Duplicate keys without override
  are boot errors.
  (src/Tapestry.Scripting/Modules/EssenceModule.cs:30-70)

- Registration also writes a theme entry under `essence.<key>` with the given
  color and html values.
  (src/Tapestry.Scripting/Modules/EssenceModule.cs:65-68)

- `essence.format(essenceKey)` produces a formatted display string; `essence.
  getEssence(key)` returns `{key, glyph, color}`.
  (src/Tapestry.Scripting/Modules/EssenceModule.cs:73-80)

- Items carry essence as the string property `essence` (engine-registered,
  applies to EntityTypes.Item).
  (src/Tapestry.Engine/Items/ItemProperties.cs:9, 14)

### Rarity tiers

- `RarityTierDefinition` is a record with `Key`, `Order` (int sort position),
  `DisplayText` (nullable), `Decorators` (nullable left/right bracket pair),
  `Color`, and `Visible` (bool).
  (src/Tapestry.Engine/Inventory/RarityTierDefinition.cs:3-10)

- Packs register rarity tiers via `rarity.register({key, order, visible,
  displayText, decorators, color, html, override})`; deferred to seal. Duplicate
  keys without override are boot errors.
  (src/Tapestry.Scripting/Modules/RarityModule.cs:30-87)

- Registration also writes a theme entry under `item.<key>` with color and html.
  (src/Tapestry.Scripting/Modules/RarityModule.cs:83-86)

- `rarity.format(key)` / `rarity.formatInline(key)` produce display strings.
  `rarity.getTier(key)` returns the tier object. `rarity.tagWidth()` returns
  the fixed width used for rarity tag padding.
  (src/Tapestry.Scripting/Modules/RarityModule.cs:89-108)

- Items carry rarity as the string property `rarity` (engine-registered, applies
  to EntityTypes.Item).
  (src/Tapestry.Engine/Items/ItemProperties.cs:8, 13)

## Rejected and Reverted

(No tombstones recorded at time of writing.)

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
