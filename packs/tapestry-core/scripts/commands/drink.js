tapestry.commands.register({
    name: 'drink',
    description: 'Drink from a container in your inventory or a source in the room.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var keyword = resolved.item;

        // Search inventory first, then room
        var item = tapestry.inventory.findByKeyword(actor.entityId, keyword);
        var fromRoom = false;
        if (!item) {
            item = tapestry.inventory.findInRoom(actor.entityId, keyword);
            fromRoom = true;
        }
        if (!item) {
            actor.send("You don't see that here.\r\n");
            return;
        }

        // Room fixtures (fountains, wells) have drinkable property
        var drinkable = tapestry.world.getProperty(item.id, 'drinkable');
        if (drinkable) {
            actor.send('You drink from ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' drinks from ' + item.name + '.\r\n');
            return;
        }

        // Inventory drinks use item_type check
        var itemType = tapestry.world.getProperty(item.id, 'item_type');
        if (itemType !== 'drink') {
            actor.send("You can't drink from that.\r\n");
            return;
        }

        var charges = tapestry.world.getProperty(item.id, 'charges');
        if (charges !== undefined && charges !== null && charges <= 0) {
            actor.send("It's empty.\r\n");
            return;
        }

        var result = tapestry.consumables.consume(actor.entityId, item.id);
        if (result && result.success) {
            actor.send('You drink from ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' drinks from ' + item.name + '.\r\n');
        } else if (result && result.reason === 'nocharges') {
            actor.send("It's empty.\r\n");
        } else {
            actor.send("You can't drink from that.\r\n");
        }
    }
});
