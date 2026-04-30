tapestry.commands.register({
    name: 'read',
    description: 'Read a sign, letter, book, or other written item',
    category: 'information',
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Read what?\r\n');
            return;
        }

        var keyword = args[0].toLowerCase();

        // Check room for readable items first
        var roomItems = tapestry.world.getEntitiesInRoom(player.roomId, 'readable');
        for (var i = 0; i < roomItems.length; i++) {
            if (roomItems[i].name.toLowerCase().indexOf(keyword) !== -1) {
                var text = tapestry.world.getProperty(roomItems[i].id, 'text');
                if (text) {
                    player.send(text + '\r\n');
                } else {
                    player.send('There is nothing written there.\r\n');
                }
                return;
            }
        }

        // Check player inventory for readable items
        var carried = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (carried) {
            var tags = tapestry.world.getEntityTags ? tapestry.world.getEntityTags(carried.id) : null;
            var isReadable = tags && tags.indexOf('readable') !== -1;
            if (isReadable) {
                var text = tapestry.world.getProperty(carried.id, 'text');
                if (text) {
                    player.send(text + '\r\n');
                } else {
                    player.send('There is nothing written there.\r\n');
                }
                return;
            }
        }

        player.send("You don't see that here.\r\n");
    }
});
