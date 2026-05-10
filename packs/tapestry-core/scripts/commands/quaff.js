tapestry.commands.register({
    name: 'quaff',
    description: 'Quaff a potion from your inventory.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'inventory', required: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        var consumeMethod = tapestry.world.getProperty(item.id, 'consume_method');
        if (consumeMethod !== 'quaff') {
            actor.send("You can't quaff that.\r\n");
            return;
        }

        var result = tapestry.consumables.consume(actor.entityId, item.id);
        if (result && result.success) {
            actor.send('You quaff ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' quaffs ' + item.name + '.\r\n');
        } else {
            actor.send("You can't quaff that.\r\n");
        }
    }
});
