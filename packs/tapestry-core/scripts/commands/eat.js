tapestry.commands.register({
    name: 'eat',
    description: 'Eat food from your inventory.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'inventory', required: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        var consumeMethod = tapestry.world.getProperty(item.id, 'consume_method');
        if (consumeMethod !== 'eat') {
            actor.send("You can't eat that.\r\n");
            return;
        }

        var result = tapestry.consumables.consume(actor.entityId, item.id);
        if (result && result.success) {
            actor.send('You eat ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' eats ' + item.name + '.\r\n');
        } else if (result && result.reason === 'nocharges') {
            actor.send("It's empty.\r\n");
        } else {
            actor.send("You can't eat that.\r\n");
        }
    }
});
