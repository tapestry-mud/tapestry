tapestry.commands.register({
    name: 'unlock',
    description: 'Unlock a door.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Unlock what? Usage: unlock [direction or door name]\r\n');
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
            player.send("There's no lock there.\r\n");
            return;
        }

        if (!door.isLocked) {
            player.send('That is not locked.\r\n');
            return;
        }

        if (door.keyId && !tapestry.doors.hasKey(player.entityId, door.keyId)) {
            player.send("You don't have the key.\r\n");
            return;
        }

        var ok = tapestry.doors.unlock(player.entityId, roomId, dirStr);
        if (ok) {
            player.send('You unlock the ' + door.name + '.\r\n');
            tapestry.world.sendToRoomExcept(roomId, player.entityId,
                player.name + ' unlocks the ' + door.name + '.\r\n');
        } else {
            player.send("You can't unlock that.\r\n");
        }
    }
});
