tapestry.commands.register({
    name: 'remove',
    description: 'Remove a worn or wielded item.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Remove what?\r\n');
            return;
        }
        var keyword = args[0];

        if (keyword === 'all') {
            var results = tapestry.equipment.unequipAll(player.entityId);
            if (!results || results.length === 0) {
                player.send("You aren't wearing anything.\r\n");
                return;
            }
            results.forEach(function(r) {
                player.send('You remove ' + r.itemName + '.\r\n');
            });
            player.sendToRoom(player.name + ' removes some equipment.\r\n');
            return;
        }

        var result = tapestry.equipment.unequipByKeyword(player.entityId, keyword);
        if (result) {
            player.send('You remove ' + result.itemName + '.\r\n');
            player.sendToRoom(player.name + ' removes ' + result.itemName + '.\r\n');
            return;
        }

        var success = tapestry.equipment.unequip(player.entityId, keyword);
        if (success) {
            player.send('You remove your equipment.\r\n');
        } else {
            player.send("You aren't wearing anything there.\r\n");
        }
    }
});
