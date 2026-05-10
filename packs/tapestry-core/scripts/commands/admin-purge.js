tapestry.commands.register({
    name: 'purge',
    description: 'Remove NPCs, items, or all entities from your room.',
    category: 'admin',
    admin: true,
    args: {
        filter: { type: 'keyword', required: false }
    },
    handler: function(actor, resolved) {
        var filter = resolved.filter ? resolved.filter.toLowerCase() : 'all';

        if (filter !== 'npc' && filter !== 'items' && filter !== 'all') {
            actor.send('Usage: purge [npc|items|all]\r\n');
            return;
        }

        var normalizedFilter = filter === 'items' ? 'item' : filter;
        var count = tapestry.world.purgeEntities(actor.roomId, normalizedFilter);
        actor.send('Purged ' + count + ' entities from room.\r\n');
    }
});
