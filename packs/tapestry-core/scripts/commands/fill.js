// packs/tapestry-core/scripts/commands/fill.js
tapestry.commands.register({
    name: 'fill',
    description: 'Fill a container from a source.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'inventory', required: true },
        source: { type: 'room_item', required: true, prepositions: ['from'] }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;
        var source = resolved.source;

        var result = tapestry.inventory.fillItem(actor.entityId, item.keyword, source.keyword);
        if (!result) {
            actor.send("You can't do that.\r\n");
            return;
        }
        if (result.success) {
            actor.send('You fill ' + result.targetName + ' from ' + result.sourceName + '.\r\n');
            actor.sendToRoom(actor.name + ' fills ' + result.targetName + '.\r\n');
        } else if (result.reason === 'not_fillable') {
            actor.send("You can't fill that.\r\n");
        } else if (result.reason === 'mixed_liquids') {
            actor.send("You can't mix liquids.\r\n");
        } else if (result.reason === 'source_empty') {
            actor.send(result.sourceName + " has dried up.\r\n");
        } else {
            actor.send("You can't do that.\r\n");
        }
    }
});
