tapestry.commands.register({
    name: 'close',
    description: 'Close a door or container.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Close what? Usage: close [direction or door name]\r\n');
            return;
        }

        var roomId = tapestry.world.getEntityRoomId(player.entityId);
        if (!roomId) { return; }

        var input = args.join(' ');
        var dirStr = tapestry.doors.resolveTarget(roomId, input);

        if (!dirStr) {
            player.send("You don't see that here, or it's ambiguous.\r\n");
            return;
        }

        var door = tapestry.doors.getDoor(roomId, dirStr);
        if (!door) {
            player.send("There's no door that way.\r\n");
            return;
        }

        if (door.isClosed) {
            player.send('That is already closed.\r\n');
            return;
        }

        var ok = tapestry.doors.close(player.entityId, roomId, dirStr);
        if (ok) {
            player.send('You close the ' + door.name + '.\r\n');
            tapestry.world.sendToRoomExcept(roomId, player.entityId,
                player.name + ' closes the ' + door.name + '.\r\n');
        } else {
            player.send("You can't close that.\r\n");
        }
    }
});
