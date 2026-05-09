tapestry.commands.register({
    name: 'lock',
    description: 'Lock a door.',
    category: 'world',
    roles: ['player'],
    args: {
        target: { type: 'door', required: true }
    },
    handler: function(actor, resolved) {
        var roomId = tapestry.world.getEntityRoomId(actor.entityId);
        if (!roomId) { return; }

        var door = resolved.target.door;
        var dirStr = resolved.target.direction;

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
