tapestry.commands.register({
    name: 'unlock',
    description: 'Unlock a door.',
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

        if (!door.isLocked) {
            actor.send('That is not locked.\r\n');
            return;
        }

        if (door.keyId && !tapestry.doors.hasKey(actor.entityId, door.keyId)) {
            actor.send("You don't have the key.\r\n");
            return;
        }

        var ok = tapestry.doors.unlock(actor.entityId, roomId, dirStr);
        if (ok) {
            actor.send('You unlock the ' + door.name + '.\r\n');
            actor.sendToRoom(actor.name + ' unlocks the ' + door.name + '.\r\n');
        } else {
            actor.send("You can't unlock that.\r\n");
        }
    }
});
