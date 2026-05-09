tapestry.commands.register({
    name: 'read',
    description: 'Read a sign, letter, book, or other written item.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'findable', required: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        var tags = tapestry.world.getEntityTags(item.id);
        if (!tags || tags.indexOf('readable') === -1) {
            actor.send("There's nothing written on that.\r\n");
            return;
        }

        var text = tapestry.world.getProperty(item.id, 'text');
        if (text) {
            actor.send(text + '\r\n');
        } else {
            actor.send('There is nothing written there.\r\n');
        }
    }
});
