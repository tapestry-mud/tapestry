tapestry.commands.register({
    name: 'drink',
    description: 'Drink from a container in your inventory or a source in the room.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'findable', required: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        var drinkable = tapestry.world.hasTag(item.id, 'drinkable');
        if (drinkable) {
            actor.send('You drink from ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' drinks from ' + item.name + '.\r\n');
            var current = tapestry.consumables.getSustenance(actor.entityId);
            tapestry.world.setProperty(actor.entityId, 'sustenance', Math.min(100, current + 15));
            return;
        }

        var consumeMethod = tapestry.world.getProperty(item.id, 'consume_method');
        if (consumeMethod !== 'drink') {
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
