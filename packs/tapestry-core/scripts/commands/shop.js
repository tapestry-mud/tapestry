// Shop commands: list, buy, sell, value
// Subcommand dispatch via structured args. Bare 'shop' lists items.
// All player-facing strings live here; tapestry.shop exposes structured result codes.

tapestry.commands.register({
    name: 'shop',
    aliases: ['list'],
    description: 'List items for sale at a shop.',
    category: 'progression',
    roles: ['player'],
    args: {
        filter: { type: 'keyword', required: false }
    },
    handler: function(actor, resolved) {
        handleList(actor, resolved.filter || null);
    }
});

tapestry.commands.register({
    name: 'buy',
    description: 'Buy an item from a shop.',
    category: 'progression',
    roles: ['player'],
    args: {
        item: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        handleBuy(actor, resolved.item);
    }
});

tapestry.commands.register({
    name: 'sell',
    description: 'Sell an item to a shop.',
    category: 'progression',
    roles: ['player'],
    args: {
        item: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        handleSell(actor, resolved.item);
    }
});

tapestry.commands.register({
    name: 'value',
    description: 'Check what a shop would pay or charge for an item.',
    category: 'progression',
    roles: ['player'],
    args: {
        item: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        handleValue(actor, resolved.item);
    }
});

function handleList(actor, filter) {
    var npcId = tapestry.shop.findShopInRoom(actor.entityId);
    if (!npcId) {
        actor.send('There is no shop here.\r\n');
        return;
    }

    var npc = tapestry.world.getEntity(npcId);
    var shopkeeperName = npc ? npc.name : 'Shopkeeper';
    var items = tapestry.shop.listings(npcId);

    if (!items || items.length === 0) {
        tapestry.gmcp.send(actor.entityId, 'Response.Shop.List', {
            status: 'ok',
            shopkeeper: shopkeeperName,
            items: [],
            filter: filter
        });
        tapestry.respond.suppress(actor.entityId);
        actor.send('The shop has nothing for sale.\r\n');
        return;
    }

    var displayed = filter
        ? items.filter(function(i) { return i.name.toLowerCase().indexOf(filter.toLowerCase()) !== -1; })
        : items;

    tapestry.gmcp.send(actor.entityId, 'Response.Shop.List', {
        status: 'ok',
        shopkeeper: shopkeeperName,
        items: displayed.map(function(i) {
            return { id: i.templateId, name: i.name, price: i.price };
        }),
        filter: filter
    });

    tapestry.respond.suppress(actor.entityId);

    if (displayed.length === 0) {
        actor.send('Nothing for sale matches that.\r\n');
        return;
    }

    var lines = displayed.map(function(i) {
        var name = i.name;
        var price = i.price + ' gold';
        var dots = '.'.repeat(Math.max(1, 50 - name.length - price.length));
        return '  ' + name + ' ' + dots + ' ' + price;
    });
    actor.send(lines.join('\r\n') + '\r\n');
}

function handleBuy(actor, query) {
    if (!query) {
        actor.send('Buy what?\r\n');
        return;
    }

    var npcId = tapestry.shop.findShopInRoom(actor.entityId);
    if (!npcId) {
        actor.send('There is no shop here.\r\n');
        return;
    }

    var result = tapestry.shop.buy(actor.entityId, npcId, query);

    var messages = {
        ok: 'You buy ' + (result.itemName || query) + ' for ' + result.amount + ' gold.',
        noShopHere: 'There is no shop here.',
        itemNotForSale: "The shopkeeper doesn't sell that.",
        insufficientGold: "You can't afford that. (" + (result.amount - result.goldRemaining) + ' gold short)',
        ambiguousItem: "Which one? Several listings match '" + query + "'."
    };
    var message = messages[result.reason] || 'Something went wrong.';

    tapestry.gmcp.send(actor.entityId, 'Response.Shop.Buy', {
        status: result.ok ? 'ok' : 'error',
        message: message,
        item: result.ok ? (result.itemName || query) : undefined,
        cost: result.ok ? result.amount : undefined,
        goldRemaining: result.ok ? result.goldRemaining : undefined
    });

    tapestry.respond.suppress(actor.entityId);
    actor.send(message + '\r\n');
}

function handleSell(actor, query) {
    if (!query) {
        actor.send('Sell what?\r\n');
        return;
    }

    var npcId = tapestry.shop.findShopInRoom(actor.entityId);
    if (!npcId) {
        actor.send('There is no shop here.\r\n');
        return;
    }

    var result = tapestry.shop.sell(actor.entityId, npcId, query);

    var messages = {
        ok: 'You sell ' + (result.itemName || query) + ' for ' + result.amount + ' gold.',
        noShopHere: 'There is no shop here.',
        itemNotInInventory: "You aren't carrying that.",
        itemIsNoSell: "The shopkeeper won't take that.",
        itemValueZero: "The shopkeeper won't take that."
    };
    var message = messages[result.reason] || 'Something went wrong.';

    tapestry.gmcp.send(actor.entityId, 'Response.Shop.Sell', {
        status: result.ok ? 'ok' : 'error',
        message: message,
        item: result.ok ? (result.itemName || query) : undefined,
        earnings: result.ok ? result.amount : undefined,
        goldRemaining: result.ok ? result.goldRemaining : undefined
    });

    tapestry.respond.suppress(actor.entityId);
    actor.send(message + '\r\n');
}

function handleValue(actor, query) {
    if (!query) {
        actor.send('Value what?\r\n');
        return;
    }

    var npcId = tapestry.shop.findShopInRoom(actor.entityId);
    if (!npcId) {
        actor.send('There is no shop here.\r\n');
        return;
    }

    var result = tapestry.shop.value(actor.entityId, npcId, query);

    var message;
    if (result.ok) {
        if (result.scope === 'inventory') {
            message = 'The shopkeeper would pay ' + result.amount + ' gold for ' + result.itemName + '.';
        } else {
            message = result.itemName + ' would cost you ' + result.amount + ' gold.';
        }
    } else if (result.reason === 'itemNotInInventory' || result.reason === 'itemNotForSale') {
        message = "You don't have that, and the shop doesn't sell it.";
    } else {
        message = 'There is no shop here.';
    }

    tapestry.gmcp.send(actor.entityId, 'Response.Shop.Value', {
        status: result.ok ? 'ok' : 'error',
        message: message,
        item: result.ok ? result.itemName : undefined,
        buyPrice: (result.ok && result.scope !== 'inventory') ? result.amount : undefined,
        sellPrice: (result.ok && result.scope === 'inventory') ? result.amount : undefined
    });

    tapestry.respond.suppress(actor.entityId);
    actor.send(message + '\r\n');
}
