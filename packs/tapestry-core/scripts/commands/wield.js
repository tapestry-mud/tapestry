tapestry.commands.register({
    name: 'wield',
    description: 'Wield a weapon from your inventory.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'inventory', required: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;
        var result = tapestry.equipment.equip(actor.entityId, item.keyword, 'wield');
        if (result) {
            if (result.displaced) {
                actor.send('You remove ' + result.displaced.name + '.\r\n');
            }
            actor.send('You wield ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' wields ' + item.name + '.\r\n');
        } else {
            actor.send("You can't wield that.\r\n");
        }
    }
});
