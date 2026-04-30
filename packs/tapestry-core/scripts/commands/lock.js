tapestry.commands.register({
    name: 'lock',
    description: 'Lock a door.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Lock what? Usage: lock [direction or door name]\r\n');
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

        if (door.isLocked) {
            player.send('That is already locked.\r\n');
            return;
        }

        if (!door.isClosed) {
            player.send('You must close it before locking.\r\n');
            return;
        }

        if (door.keyId && !tapestry.doors.hasKey(player.entityId, door.keyId)) {
            player.send("You don't have the key.\r\n");
            return;
        }

        var ok = tapestry.doors.lockDoor(player.entityId, roomId, dirStr);
        if (ok) {
            player.send('You lock the ' + door.name + '.\r\n');
            tapestry.world.sendToRoomExcept(roomId, player.entityId,
                player.name + ' locks the ' + door.name + '.\r\n');
        } else {
            player.send("You can't lock that.\r\n");
        }
    }
});
