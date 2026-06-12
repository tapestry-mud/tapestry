---
capability: economy-and-shops
last-updated: 2026-06-12
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

- **[ShopConfig.YAML]** Shop configuration is declared in a mob YAML file in three
  supported forms:
  1. Nested block -- `shop: { sells: [...], buy_markup: 1.4, sell_discount: 0.3 }`
  2. Flat top-level field -- `shop_sells: [...]` (markup/discount fall back to server
     defaults when omitted or 0).
  3. Legacy dotted property -- a `shop.sells` key in the `properties` map (only parsed
     when the entity has the `shop` tag and `shop_sells` is empty). Markup/discount fall
     back to server defaults.
  `src/Tapestry.Scripting/PackLoader.cs` (lines 421-460)

- **[ShopConfig.Validation]** The pack validator emits a warning (not an error) when a
  mob has the `shop` tag but its `ShopConfig` is null or its sells list is empty.
  `src/Tapestry.Scripting/PackValidator.cs`

- **[ShopConfig.Stock]** Stock is a static ordered list of item template IDs. There is no
  runtime stock refresh or quantity tracking. Items missing from `ItemRegistry` are
  silently skipped when building the listing. Items whose base `value` is <= 0 are also
  skipped; they will not appear in `GetListings` output.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`GetListings`, lines 63-66)

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

- **[ShopSell.Flow]** `Sell` resolves an inventory item by prefix match and rejects items
  tagged `no_sell` (reason `ItemIsNoSell`) or with value <= 0 (reason `ItemValueZero`).
  It then publishes a cancellable `shop.sell` event; if the event is cancelled the result
  reason is `ItemNotForSale`. Only after the event survives does the service auto-unequip
  the item if worn/wielded, remove it from the world, and credit the sell price to the
  player.
  `src/Tapestry.Engine/Economy/ShopService.cs` (`Sell`, lines 174-190)

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
  `packs/@tapestry/core/scripts/commands/shop.js`

- **[ShopPack.Example]** The example pack ships a working shop mob at
  `packs/@tapestry/example-pack/areas/starter-town/mobs/merchant.yaml` (id
  `tapestry-example-pack:merchant`, tag `shop`, uses the flat `shop_sells` config form
  with ten stock items).

---

## Rejected and Reverted

No reversals on record as of 2026-06-12.

---

## Change Log

| Change Record | Summary |
|---------------|---------|
