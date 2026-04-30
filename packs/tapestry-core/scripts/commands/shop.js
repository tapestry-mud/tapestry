// Shop commands: list, buy, sell, value
// All player-facing strings live here; tapestry.shop exposes structured result codes.

tapestry.commands.register({
    name: 'list',
    description: 'List items for sale in a shop.',
    handler: function(player, args) {
        var npcId = tapestry.shop.findShopInRoom(player.entityId);
        if (!npcId) { player.send('There is no shop here.\r\n'); return; }
        var items = tapestry.shop.listings(npcId);
        if (!items || items.length === 0) { player.send('The shop has nothing for sale.\r\n'); return; }
        var lines = items.map(function(item) {
            var name = item.name;
            var price = item.price + ' gold';
            var dots = '.'.repeat(Math.max(1, 50 - name.length - price.length));
            return '  ' + name + ' ' + dots + ' ' + price;
        });
        player.send(lines.join('\r\n') + '\r\n');
    }
});

tapestry.commands.register({
    name: 'buy',
    description: 'Buy an item from a shop.',
    handler: function(player, args) {
        if (args.length === 0) { player.send('Buy what?\r\n'); return; }
        var npcId = tapestry.shop.findShopInRoom(player.entityId);
        if (!npcId) { player.send('There is no shop here.\r\n'); return; }
        var query = args.join(' ');
        var result = tapestry.shop.buy(player.entityId, npcId, query);
        var messages = {
            ok: 'You buy ' + (result.itemName || query) + ' for ' + result.amount + ' gold.',
            noShopHere: 'There is no shop here.',
            itemNotForSale: "The shopkeeper doesn't sell that.",
            insufficientGold: "You can't afford that. (" + (result.amount - result.goldRemaining) + ' gold short)',
            ambiguousItem: "Which one? Several listings match '" + query + "'."
        };
        player.send((messages[result.reason] || "Something went wrong.") + '\r\n');
    }
});

tapestry.commands.register({
    name: 'sell',
    description: 'Sell an item to a shop.',
    handler: function(player, args) {
        if (args.length === 0) { player.send('Sell what?\r\n'); return; }
        var npcId = tapestry.shop.findShopInRoom(player.entityId);
        if (!npcId) { player.send('There is no shop here.\r\n'); return; }
        var query = args.join(' ');
        var result = tapestry.shop.sell(player.entityId, npcId, query);
        var messages = {
            ok: 'You sell ' + (result.itemName || query) + ' for ' + result.amount + ' gold.',
            noShopHere: 'There is no shop here.',
            itemNotInInventory: "You aren't carrying that.",
            itemIsNoSell: "The shopkeeper won't take that.",
            itemValueZero: "The shopkeeper won't take that."
        };
        player.send((messages[result.reason] || "Something went wrong.") + '\r\n');
    }
});

tapestry.commands.register({
    name: 'value',
    description: 'Check how much a shop will pay for an item.',
    handler: function(player, args) {
        if (args.length === 0) { player.send('Value what?\r\n'); return; }
        var npcId = tapestry.shop.findShopInRoom(player.entityId);
        if (!npcId) { player.send('There is no shop here.\r\n'); return; }
        var query = args.join(' ');
        var result = tapestry.shop.value(player.entityId, npcId, query);
        if (result.reason === 'ok') {
            if (result.scope === 'inventory') {
                player.send('The shopkeeper would pay ' + result.amount + ' gold for ' + result.itemName + '.\r\n');
            } else {
                player.send(result.itemName + ' would cost you ' + result.amount + ' gold.\r\n');
            }
        } else if (result.reason === 'itemNotInInventory' || result.reason === 'itemNotForSale') {
            player.send("You don't have that, and the shop doesn't sell it.\r\n");
        } else {
            player.send('There is no shop here.\r\n');
        }
    }
});
