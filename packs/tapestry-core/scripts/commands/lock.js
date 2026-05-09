tapestry.commands.register({
    name: 'lock',
    description: 'Lock a door.',
    category: 'world',
    roles: ['player'],
    args: {
        target: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var input = resolved.target;

        var roomId = tapestry.world.getEntityRoomId(actor.entityId);
        if (!roomId) { return; }

        var dirStr = tapestry.doors.resolveTarget(roomId, input);

        if (!dirStr) {
            actor.send("You don't see that here, or it's ambiguous. Try specifying a direction or ordinal (e.g., 2.door).\r\n");
            return;
        }

        var door = tapestry.doors.getDoor(roomId, dirStr);
        if (!door) {
            actor.send("There's no lock there.\r\n");
            return;
        }

        if (door.isLocked) {
            actor.send('That is already locked.\r\n');
            return;
        }

        if (!door.isClosed) {
            actor.send('You must close it before locking.\r\n');
            return;
        }

        if (door.keyId && !tapestry.doors.hasKey(actor.entityId, door.keyId)) {
            actor.send("You don't have the key.\r\n");
            return;
        }

        var ok = tapestry.doors.lockDoor(actor.entityId, roomId, dirStr);
        if (ok) {
            actor.send('You lock the ' + door.name + '.\r\n');
            actor.sendToRoom(actor.name + ' locks the ' + door.name + '.\r\n');
        } else {
            actor.send("You can't lock that.\r\n");
        }
    }
});
