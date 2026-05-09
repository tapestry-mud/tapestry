tapestry.commands.register({
    name: 'open',
    description: 'Open a door or container.',
    category: 'world',
    roles: ['player', 'mob'],
    args: {
        target: { type: 'door', required: true }
    },
    handler: function(actor, resolved) {
        var roomId = tapestry.world.getEntityRoomId(actor.entityId);
        if (!roomId) { return; }

        var door = resolved.target.door;
        var dirStr = resolved.target.direction;

        if (!door.isClosed) {
            actor.send('That is already open.\r\n');
            return;
        }

        if (door.isLocked) {
            actor.send('That is locked.\r\n');
            return;
        }

        var ok = tapestry.doors.open(actor.entityId, roomId, dirStr);
        if (ok) {
            actor.send('You open the ' + door.name + '.\r\n');
            actor.sendToRoom(actor.name + ' opens the ' + door.name + '.\r\n');
        } else {
            actor.send("You can't open that.\r\n");
        }
    }
});
