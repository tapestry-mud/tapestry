tapestry.commands.register({
    name: 'open',
    description: 'Open a door or container.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Open what? Usage: open [direction or door name]\r\n');
            return;
        }

        var roomId = tapestry.world.getEntityRoomId(player.entityId);
        if (!roomId) { return; }

        var input = args.join(' ');
        var dirStr = tapestry.doors.resolveTarget(roomId, input);

        if (!dirStr) {
            player.send("You don't see that here, or it's ambiguous. Try specifying a direction or ordinal (e.g., 2.door).\r\n");
            return;
        }

        var door = tapestry.doors.getDoor(roomId, dirStr);
        if (!door) {
            player.send("There's no door that way.\r\n");
            return;
        }

        if (!door.isClosed) {
            player.send('That is already open.\r\n');
            return;
        }

        if (door.isLocked) {
            player.send('That is locked.\r\n');
            return;
        }

        var ok = tapestry.doors.open(player.entityId, roomId, dirStr);
        if (ok) {
            player.send('You open the ' + door.name + '.\r\n');
            tapestry.world.sendToRoomExcept(roomId, player.entityId,
                player.name + ' opens the ' + door.name + '.\r\n');
        } else {
            player.send("You can't open that.\r\n");
        }
    }
});
