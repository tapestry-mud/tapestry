tapestry.commands.register({
    name: 'enter',
    description: 'Enter a portal or special exit.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Enter what? Usage: enter [keyword]\r\n');
            return;
        }

        var roomId = tapestry.world.getEntityRoomId(player.entityId);
        if (!roomId) { return; }

        var keyword = args.join(' ').toLowerCase();
        var exits = tapestry.portals.getKeywordExits(roomId);
        var match = null;

        for (var i = 0; i < exits.length; i++) {
            if (exits[i].keyword.toLowerCase() === keyword) {
                match = exits[i];
                break;
            }
        }

        if (!match) {
            player.send("You don't see that here.\r\n");
            return;
        }

        if (match.door) {
            if (match.door.isLocked) {
                player.send('That is locked.\r\n');
                return;
            }
            if (match.door.isClosed) {
                player.send('That is closed.\r\n');
                return;
            }
        }

        tapestry.world.sendToRoomExcept(roomId, player.entityId,
            player.name + ' passes through the ' + match.name + '.\r\n');

        tapestry.world.teleportEntity(player.entityId, match.targetRoomId);
        tapestry.world.sendRoomDescription(player.entityId);
    }
});
