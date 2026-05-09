tapestry.commands.register({
    name: 'close',
    description: 'Close a door or container.',
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

        if (door.isClosed) {
            actor.send('That is already closed.\r\n');
            return;
        }

        var ok = tapestry.doors.close(actor.entityId, roomId, dirStr);
        if (ok) {
            actor.send('You close the ' + door.name + '.\r\n');
            actor.sendToRoom(actor.name + ' closes the ' + door.name + '.\r\n');
        } else {
            actor.send("You can't close that.\r\n");
        }
    }
});
