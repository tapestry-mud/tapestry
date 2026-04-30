tapestry.commands.register({
    name: 'purge',
    admin: true,
    description: 'Remove NPCs, items, or all entities from your room.',
    priority: 0,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        var filter = 'all';
        if (args.length > 0) {
            var arg = args[0].toLowerCase();
            if (arg === 'npc' || arg === 'items' || arg === 'all') {
                filter = arg === 'items' ? 'item' : arg;
            } else {
                player.send('Usage: purge [npc|items|all]\r\n');
                return;
            }
        }
        var count = tapestry.world.purgeEntities(player.roomId, filter);
        player.send('Purged ' + count + ' entities from room.\r\n');
    }
});
