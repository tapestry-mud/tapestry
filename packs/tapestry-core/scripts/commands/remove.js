tapestry.commands.register({
    name: 'remove',
    description: 'Remove a worn or wielded item.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var keyword = resolved.item;

        if (keyword === 'all') {
            var results = tapestry.equipment.unequipAll(actor.entityId);
            if (!results || results.length === 0) {
                actor.send("You aren't wearing anything.\r\n");
                return;
            }
            results.forEach(function(r) {
                actor.send('You remove ' + r.itemName + '.\r\n');
            });
            actor.sendToRoom(actor.name + ' removes some equipment.\r\n');
            return;
        }

        var result = tapestry.equipment.unequipByKeyword(actor.entityId, keyword);
        if (result) {
            actor.send('You remove ' + result.itemName + '.\r\n');
            actor.sendToRoom(actor.name + ' removes ' + result.itemName + '.\r\n');
            return;
        }

        var success = tapestry.equipment.unequip(actor.entityId, keyword);
        if (success) {
            actor.send('You remove your equipment.\r\n');
        } else {
            actor.send("You aren't wearing anything there.\r\n");
        }
    }
});
