tapestry.commands.register({
    name: 'recite',
    description: 'Recite a scroll from your inventory.',
    category: 'social',
    roles: ['player'],
    args: {
        item: { type: 'inventory', required: true },
        target: { type: 'entity', required: false }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;
        var target = resolved.target;

        var consumeMethod = tapestry.world.getProperty(item.id, 'consume_method');
        if (consumeMethod !== 'recite') {
            actor.send("You can't recite that.\r\n");
            return;
        }

        var result = tapestry.consumables.consume(actor.entityId, item.id);
        if (result && result.success) {
            if (target) {
                actor.send('You recite ' + item.name + ' at ' + target.name + '.\r\n');
                tapestry.world.send(target.id, actor.name + ' recites ' + item.name + ' at you.\r\n');
                actor.sendToRoom(actor.name + ' recites ' + item.name + ' at ' + target.name + '.\r\n');
            } else {
                actor.send('You recite ' + item.name + '.\r\n');
                actor.sendToRoom(actor.name + ' recites ' + item.name + '.\r\n');
            }
        } else {
            actor.send("You can't recite that.\r\n");
        }
    }
});
