tapestry.commands.register({
    name: 'drink',
    description: 'Drink from an item in your inventory.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'inventory', required: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        var itemType = tapestry.world.getProperty(item.id, 'item_type');
        if (itemType !== 'drink') {
            actor.send("You can't drink that.\r\n");
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
            actor.send("You can't drink that.\r\n");
        }
    }
});
