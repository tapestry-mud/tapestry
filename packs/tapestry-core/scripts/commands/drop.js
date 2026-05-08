tapestry.commands.register({
    name: 'drop',
    description: 'Drop an item from your inventory.',
    category: 'inventory',
    roles: ['player', 'mob'],
    args: {
        item: { type: 'inventory', required: true, bulk: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        if (Array.isArray(item)) {
            if (item.length === 0) {
                actor.send("You aren't carrying anything to drop.\r\n");
                return;
            }
            item.forEach(function(i) {
                tapestry.inventory.drop(actor.entityId, i.keyword);
                actor.send('You drop ' + i.name + '.\r\n');
            });
            actor.sendToRoom(actor.name + ' drops some items.\r\n');
            return;
        }

        var success = tapestry.inventory.drop(actor.entityId, item.keyword);
        if (success) {
            actor.send('You drop ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' drops ' + item.name + '.\r\n');
        }
    }
});
