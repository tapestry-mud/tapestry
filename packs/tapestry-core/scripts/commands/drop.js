tapestry.commands.register({
    name: 'drop',
    description: 'Drop an item from your inventory.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Drop what?\r\n');
            return;
        }
        var keyword = args[0];

        if (keyword === 'all' || keyword.indexOf('all.') === 0) {
            var results = tapestry.inventory.dropAll(player.entityId, keyword);
            if (!results || results.length === 0) {
                player.send("You aren't carrying anything to drop.\r\n");
                return;
            }
            results.forEach(function(r) {
                player.send('You drop ' + r.name + '.\r\n');
            });
            if (results.length > 0) {
                player.sendToRoom(player.name + ' drops some items.\r\n');
            }
            return;
        }

        var found = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!found) {
            player.send("You aren't carrying that.\r\n");
            return;
        }
        var success = tapestry.inventory.drop(player.entityId, keyword);
        if (success) {
            player.send('You drop ' + found.name + '.\r\n');
            player.sendToRoom(player.name + ' drops ' + found.name + '.\r\n');
        }
    }
});
