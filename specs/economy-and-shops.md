---
capability: economy-and-shops
last-updated: 2026-06-12
---

# Economy and Shops

## Overview

The economy system provides a single currency (gold), a shop mechanic attached to NPC
entities, and a rest-state system that multiplies HP/resource regeneration. All three
subsystems are data-driven: packs define item values, shopkeeper stock lists, and room
healing rates in YAML; pack scripts call into three JS modules (`currency`, `shop`,
`rest`). No game content is hardcoded in the engine.

---

## Behavior

### Currency model

- **[CurrencyProperties.Gold]** Every entity carries a `gold` integer property
  (registered engine property, `PropertyValueType.Int`).
  `src/Tapestry.Engine/Economy/CurrencyProperties.cs`

- **[CurrencyProperties.Value]** Items declare their base worth via a `value` property.
  The loader accepts int, long, double, or a parseable string; anything else resolves to 0.
  `src/Tapestry.Engine/Economy/CurrencyService.cs` (`GetItemValue`)

- **[CurrencyProperties.CurrencyTag]** Items tagged `currency` are auto-converted on
  pickup for player entities: the item is destroyed and its `value` is credited as gold.
  Non-player entities are not auto-converted.
  `src/Tapestry.Engine/Economy/CurrencyService.cs` (`TryAutoConvert`)

- **[CurrencyService.Gold.Floor]** `AddGold` clamps the result to a minimum of 0; gold
  can never go negative. `SetGold` throws `ArgumentOutOfRangeException` for negative
  inputs.
  `src/Tapestry.Engine/Economy/CurrencyService.cs`

- **[CurrencyService.Events]** Every `AddGold` call publishes `currency.credited` (delta
  >= 0) or `currency.debited` (delta < 0). `SetGold` always publishes `currency.credited`.
  Event data includes `playerId`, `amount` (absolute value), `source`, and `reason`.
  `src/Tapestry.Engine/Economy/CurrencyService.cs`

- **[CurrencyModule.JS]** Pack scripts access currency via the `currency` JS namespace:
  `currency.getGold(entityId)`, `currency.addGold(entityId, amount, reason)`,
  `currency.setGold(entityId, amount, reason)`. All return the new gold total (or 0 on
  invalid ID).
  `src/Tapestry.Scripting/Modules/CurrencyModule.cs`

### Shop model

- **[ShopTag]** An NPC is a shop if it carries the `shop` tag. `ShopService.IsShop`
  checks for this tag.
  `src/Tapestry.Engine/Economy/ShopProperties.cs`; `src/Tapestry.Engine/Economy/ShopService.cs`

- **[ShopConfig.YAML]** Shop configuration is declared in a mob YAML file in two
  supported forms:
  1. Nested block -- `shop: { sells: [...], buy_markup: 1.4, sell_discount: 0.3 }`
  2. Flat top-level field -- `shop_sells: [...]` (markup/discount fall back to server
     defaults when omitted or 0).
  `src/Tapestry.Scripting/PackLoader.cs` (lines 421-460)

- **[ShopConfig.Validation]** The pack validator emits a warning (not an error) when a
  mob has the `shop` tag but its `ShopConfig` is null or its sells list is empty.
  `src/Tapestry.Scripting/PackValidator.cs`

- **[ShopConfig.Stock]** Stock is a static ordered list of item template IDs. There is no
  runtime stock refresh or quantity tracking. Items missing from `ItemRegistry` are
  silently skipped when building the listing.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`GetListings`)

- **[ShopPricing.Buy]** Buy price = `round(itemValue * markup)`, minimum 1. The markup
  applied is the per-shop `BuyMarkup` when > 0, otherwise the server-wide
  `EconomyConfig.ShopBuyMarkup` (default 1.2).
  `src/Tapestry.Engine/Economy/ShopService.cs` (`ComputeBuyPrice`);
  `src/Tapestry.Engine/Economy/EconomyConfig.cs`

- **[ShopPricing.Sell]** Sell price = `round(itemValue * discount)`, minimum 1. Discount
  source selection mirrors the buy-markup logic; server default is 0.5.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`ComputeSellPrice`)

- **[ShopBuy.Flow]** `Buy` checks player gold, publishes a cancellable `shop.buy` event,
  deducts gold, instantiates a new item from `ItemRegistry`, and places it in the player's
  inventory. If `shop.buy` is cancelled the result reason is `ItemNotForSale`.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`Buy`)

- **[ShopSell.Flow]** `Sell` resolves an inventory item by prefix match, rejects items
  tagged `no_sell` (reason `ItemIsNoSell`) or with value <= 0 (reason `ItemValueZero`),
  auto-unequips the item if worn, publishes a cancellable `shop.sell` event, destroys the
  item, and credits the sell price to the player.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`Sell`)

- **[ShopValue]** `Value` performs an inventory-first lookup: if the player carries a
  matching item it returns the sell price with scope `inventory`; otherwise it falls back
  to the shop stock and returns the buy price with scope `stock`.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`Value`)

- **[ShopQuery.Disambiguation]** Item queries strip leading articles (a/an/the) and
  match by prefix against item name or template short ID (portion after the last `:`).
  An ambiguous query (two or more matches) returns `(null, null, 0)` internally,
  surfacing as `ItemNotForSale` to callers. Inventory query uses name prefix only.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`ResolveStockItem`, `ResolveInventoryItem`)

- **[ShopResults]** All operations return value-record results:
  `ShopBuyResult(Reason, ItemId, Price, PlayerGold)`,
  `ShopSellResult(Reason, ItemName, Price, PlayerGold)`,
  `ShopListing(TemplateId, Name, BuyPrice)`.
  `src/Tapestry.Engine/Economy/ShopResults.cs`

- **[ShopModule.JS]** Pack scripts access shops via the `shop` JS namespace:
  `shop.isShop(entityId)`, `shop.findShopInRoom(playerId)`, `shop.listings(npcId)`,
  `shop.buy(playerId, npcId, query)`, `shop.sell(playerId, npcId, query)`,
  `shop.value(playerId, npcId, query)`. All return plain JS objects. `buy` result includes
  `itemId` and `itemName`; `sell` result includes `itemName`. Reason strings are
  camelCase.
  `src/Tapestry.Scripting/Modules/ShopModule.cs`

- **[ShopFind.Room]** `FindShopInRoom` returns the first entity in the player's room that
  carries the `shop` tag, or null if none exists or the player has no location.
  `src/Tapestry.Engine/Economy/ShopService.cs`

### Rest system

- **[RestStates]** An entity has one of three rest states stored in the transient property
  `rest_state`: `awake` (default), `resting`, `sleeping`.
  `src/Tapestry.Engine/Rest/RestProperties.cs`; `src/Tapestry.Engine/Rest/RestService.cs`

- **[RestService.Transition]** `SetRestState` publishes a cancellable
  `entity.rest_state.changed` event before applying the new state. If cancelled, no
  change is made. Transitioning to `awake` clears `rest_target`. Transitioning to
  `sleeping` records the current tick in `sleep_start_tick`.
  `src/Tapestry.Engine/Rest/RestService.cs`

- **[RestService.Furniture]** A furniture entity ID may be passed to `SetRestState`; when
  set, its ID is stored in `rest_target` and its `rest_bonus` (int) is added to the
  entity's regen multiplier during the tick.
  `src/Tapestry.Engine/Rest/RestService.cs`; `src/Tapestry.Engine/GameLoop.cs` (line 467)

- **[RestConfig.Multipliers]** The regen multiplier applied per tick is:
  `awake` -> 1.0, `resting` -> 2.0 (default), `sleeping` -> 3.0 (default). Defaults are
  configurable via `RestConfig.Configure`.
  `src/Tapestry.Engine/Rest/RestConfig.cs`

- **[RestConfig.RoomBonus]** If an entity has a location, the room's `healing_rate`
  property (int, registered for `EntityTypes.Room`) is added to the multiplier. This
  stacks additively with furniture bonus and state multiplier.
  `src/Tapestry.Engine/GameLoop.cs` (line 476);
  `src/Tapestry.Engine/Rest/RestProperties.cs`

- **[RestConfig.MinSleep]** `RestConfig` stores `MinSleepTicksForWellRested` (default
  120). UNVERIFIED: no call site consuming this field was found in the current codebase;
  it may be reserved for a future "well rested" bonus.
  `src/Tapestry.Engine/Rest/RestConfig.cs`

- **[RestModule.JS]** Pack scripts access rest via the `rest` JS namespace:
  `rest.getRestState(entityId)` returns the state string (defaults to `"awake"`);
  `rest.setRestState(entityId, newState, furnitureId?)` returns `{ success, reason }`.
  `src/Tapestry.Scripting/Modules/RestModule.cs`

- **[RestRegen.Integration]** `GameLoop.RegisterRegenHandler` applies the rest multiplier
  to `regen_hp`, `regen_resource`, and `regen_movement` values each regen interval. Mobs
  and players tagged `no_regen` are excluded. A final multiplier of 0.0 skips the entity.
  `src/Tapestry.Engine/GameLoop.cs` (lines 445-486)

---

## Rejected and Reverted

No reversals on record as of 2026-06-12.

---

## Change Log

| Change Record | Summary |
|---------------|---------|

---

## Sources consulted

- `src/Tapestry.Engine/Economy/CurrencyService.cs`
- `src/Tapestry.Engine/Economy/CurrencyProperties.cs`
- `src/Tapestry.Engine/Economy/EconomyConfig.cs`
- `src/Tapestry.Engine/Economy/ShopService.cs`
- `src/Tapestry.Engine/Economy/ShopConfig.cs`
- `src/Tapestry.Engine/Economy/ShopProperties.cs`
- `src/Tapestry.Engine/Economy/ShopResults.cs`
- `src/Tapestry.Engine/Rest/RestService.cs`
- `src/Tapestry.Engine/Rest/RestConfig.cs`
- `src/Tapestry.Engine/Rest/RestProperties.cs`
- `src/Tapestry.Scripting/Modules/ShopModule.cs`
- `src/Tapestry.Scripting/Modules/CurrencyModule.cs`
- `src/Tapestry.Scripting/Modules/RestModule.cs`
- `src/Tapestry.Scripting/PackLoader.cs` (shop config loading, lines 421-460)
- `src/Tapestry.Scripting/PackValidator.cs` (shop tag validation)
- `src/Tapestry.Engine/Mobs/MobTemplate.cs`
- `src/Tapestry.Engine/GameLoop.cs` (regen handler, lines 445-486)
- `tests/Tapestry.Engine.Tests/Economy/ShopServiceTests.cs`
- `git log --oneline -15 -- src/Tapestry.Engine/Economy/ src/Tapestry.Engine/Rest/`
- No pack YAML examples found (packs/ directory is empty in this repo)

UNVERIFIED count: 1
- `RestConfig.MinSleepTicksForWellRested` -- defined and configurable but no consuming
  call site found in the codebase.

Out-of-scope notes:
- Item templates, item tags, equipment slots, and loot tables are covered by
  items-and-equipment.md.
- Gold persistence (the `gold` property is NOT transient and survives save/load) is
  covered by persistence.md.
