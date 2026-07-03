---
capability: economy-and-shops
last-updated: 2026-07-03
---

# Economy and Shops

## Overview

The economy system provides a single currency (gold) and a shop mechanic attached to NPC
entities. Both subsystems are data-driven: packs define item values and shopkeeper stock
lists in YAML; pack scripts call into two JS modules (`currency`, `shop`). No game content
is hardcoded in the engine. Rest and regeneration are covered in rest-and-recovery.md.

---

## Behavior

### Currency model

- **[CurrencyProperties.Gold]** Every entity carries a `gold` integer property
  (registered engine property, `PropertyValueType.Int`).
  (src/Tapestry.Engine/Economy/CurrencyProperties.cs)

- **[CurrencyProperties.Value]** Items declare their base worth via a `value` property.
  The loader accepts int, long, double, or a parseable string; anything else resolves to 0.
  (src/Tapestry.Engine/Economy/CurrencyService.cs)

- **[CurrencyProperties.CurrencyTag]** Items tagged `currency` are auto-converted on
  pickup for player entities: the item is destroyed and its `value` is credited as gold.
  Conversion also requires `value` > 0; a zero- or invalid-value currency item is picked
  up as a normal item. Non-player entities are not auto-converted.
  (src/Tapestry.Engine/Economy/CurrencyService.cs)

- **[CurrencyService.Gold.Floor]** `AddGold` clamps the result to a minimum of 0; gold
  can never go negative. `SetGold` throws `ArgumentOutOfRangeException` for negative
  inputs.
  (src/Tapestry.Engine/Economy/CurrencyService.cs)

- **[CurrencyService.Events]** Every `AddGold` call publishes `currency.credited` (delta
  >= 0) or `currency.debited` (delta < 0). `SetGold` always publishes `currency.credited`.
  Event data includes `playerId`, `amount` (absolute value), `source`, and `reason`.
  (src/Tapestry.Engine/Economy/CurrencyService.cs)

- **[CurrencyModule.JS]** Pack scripts access currency via the `currency` JS namespace:
  `currency.getGold(entityId)`, `currency.addGold(entityId, amount, reason)`,
  `currency.setGold(entityId, amount, reason)`. All return the new gold total (or 0 on
  invalid ID).
  (src/Tapestry.Scripting/Modules/CurrencyModule.cs)

### Shop model

- **[ShopTag]** An NPC is a shop if it carries the `shop` tag. `ShopService.IsShop`
  checks for this tag.
  (src/Tapestry.Engine/Economy/ShopProperties.cs; src/Tapestry.Engine/Economy/ShopService.cs)

- **[ShopConfig.YAML]** Shop configuration is declared in a mob YAML file via a single
  flat form, parsed only when the entity carries the `shop` tag:
  - `shop_sells: [...]` -- top-level field listing item template IDs.
  - `shop_buy_modifier` / `shop_sell_modifier` -- optional per-entity properties (in the
    `properties` map) overriding the server-wide buy/sell multipliers; fall back to
    server defaults when omitted or 0.
  The legacy nested `shop: { sells, buy_markup, sell_discount }` block and the legacy
  dotted `shop.sells` property key are retired and no longer parsed.
  (src/Tapestry.Scripting/PackLoader.cs:432-444)

- **[ShopConfig.Validation]** The pack validator emits a warning (not an error) when a
  mob has the `shop` tag but its `ShopConfig` is null or its sells list is empty.
  (src/Tapestry.Scripting/PackValidator.cs)

- **[ShopConfig.Stock]** Stock is a static ordered list of item template IDs. There is no
  runtime stock refresh or quantity tracking. Items missing from `ItemRegistry` are
  silently skipped when building the listing. Items whose base `value` is <= 0 are also
  skipped; they will not appear in `GetListings` output.
  (src/Tapestry.Engine/Economy/ShopService.cs:63-66)

- **[ShopPricing.Buy]** Buy price = `round(itemValue * markup)`, minimum 1. The markup
  applied is the per-shop `ShopConfig.BuyModifier` when > 0, otherwise the server-wide
  `EconomyConfig.ShopBuyMarkup` (default 1.2).
  (src/Tapestry.Engine/Economy/ShopService.cs; src/Tapestry.Engine/Economy/EconomyConfig.cs)

- **[ShopPricing.Sell]** Sell price = `round(itemValue * discount)`, minimum 1. Discount
  source selection mirrors the buy-markup logic (`ShopConfig.SellModifier` when > 0,
  otherwise `EconomyConfig.ShopSellDiscount`); server default is 0.5.
  (src/Tapestry.Engine/Economy/ShopService.cs)

- **[ShopBuy.Flow]** `Buy` checks player gold, publishes a cancellable `shop.buy` event,
  deducts gold, instantiates a new item from `ItemRegistry`, and places it in the player's
  inventory. If `shop.buy` is cancelled the result reason is `ItemNotForSale`.
  (src/Tapestry.Engine/Economy/ShopService.cs)

- **[ShopSell.Flow]** `Sell` resolves an inventory item by prefix match and rejects items
  tagged `no_sell` (reason `ItemIsNoSell`) or with value <= 0 (reason `ItemValueZero`).
  It then publishes a cancellable `shop.sell` event; if the event is cancelled the result
  reason is `ItemNotForSale`. Only after the event survives does the service auto-unequip
  the item if worn/wielded, remove it from the world, and credit the sell price to the
  player.
  (src/Tapestry.Engine/Economy/ShopService.cs:174-190)

- **[ShopValue]** `Value` performs an inventory-first lookup: if the player carries a
  matching item it returns the sell price with scope `inventory`; otherwise it falls back
  to the shop stock and returns the buy price with scope `stock`.
  (src/Tapestry.Engine/Economy/ShopService.cs)

- **[ShopQuery.Disambiguation]** Item queries strip leading articles (a/an/the) and
  match by prefix against item name or template short ID (portion after the last `:`).
  An ambiguous query (two or more matches) returns `(null, null, 0)` internally,
  surfacing as `ItemNotForSale` to callers. Inventory query uses name prefix only.
  (src/Tapestry.Engine/Economy/ShopService.cs)

- **[ShopResults]** All operations return value-record results:
  `ShopBuyResult(Reason, ItemId, Price, PlayerGold)`,
  `ShopSellResult(Reason, ItemName, Price, PlayerGold)`,
  `ShopListing(TemplateId, Name, BuyPrice)`.
  (src/Tapestry.Engine/Economy/ShopResults.cs)

- **[ShopModule.JS]** Pack scripts access shops via the `shop` JS namespace:
  `shop.isShop(entityId)`, `shop.findShopInRoom(playerId)`, `shop.listings(npcId)`,
  `shop.buy(playerId, npcId, query)`, `shop.sell(playerId, npcId, query)`,
  `shop.value(playerId, npcId, query)`. All return plain JS objects. `buy` result includes
  `itemId` and `itemName`; `sell` result includes `itemName`. Reason strings are
  camelCase.
  (src/Tapestry.Scripting/Modules/ShopModule.cs)

- **[ShopFind.Room]** `FindShopInRoom` returns the first entity in the player's room that
  carries the `shop` tag, or null if none exists or the player has no location.
  (src/Tapestry.Engine/Economy/ShopService.cs)

- **[ShopCommands.PlayerFacing]** The four player-facing shop commands are registered by
  `packs/@tapestry/core/scripts/commands/shop.js`:
  - `shop` / `list [filter]` -- calls `shop.listings(npcId)` and prints a formatted price
    list; an optional keyword argument filters by item name. Also sends a
    `Response.Shop.List` GMCP message.
  - `buy <item>` -- calls `shop.buy(...)` and prints the outcome; sends
    `Response.Shop.Buy` GMCP.
  - `sell <item>` -- calls `shop.sell(...)` and prints the outcome; sends
    `Response.Shop.Sell` GMCP.
  - `value <item>` -- calls `shop.value(...)` and prints either what the shop would pay
    (inventory scope) or what the item costs to buy (stock scope); sends
    `Response.Shop.Value` GMCP.
  (packs/@tapestry/core/scripts/commands/shop.js)

- **[ShopPack.Example]** The example pack ships a working shop mob at
  `packs/@tapestry/example-pack/areas/starter-town/mobs/merchant.yaml` (id
  `tapestry-example-pack:merchant`, tag `shop`, uses the flat `shop_sells` config form
  with a 13-item stock list, including two cross-pack `tapestry-cooking:*` entries).

---

## Rejected and Reverted

- None on record.

---

## Change Log

- 2026-07-03 [vocabulary-consolidation](changes/2026-07-03-vocabulary-consolidation.md) - value moved to an engine-registered property; flat shop_sells plus shop_buy_modifier/shop_sell_modifier replace the dotted shop keys and the three-spelling loader shim
- 2026-07-03: Shop key vocabulary consolidation (Slice 3, Task 3.2). Collapsed the
  three-spelling shop config shim in `PackLoader.cs` to the flat `shop_sells` form
  only; retired the legacy dotted `shop.sells` property key and the nested
  `shop: { sells, buy_markup, sell_discount }` block. Renamed the per-entity
  markup/discount override keys from `shop.buy_markup` / `shop.sell_discount` to
  `shop_buy_modifier` / `shop_sell_modifier`, and the `ShopConfig` record fields from
  `BuyMarkup` / `SellDiscount` to `BuyModifier` / `SellModifier` to match. No content
  used the retired forms.
