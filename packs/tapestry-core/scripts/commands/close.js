tapestry.commands.register({
    name: 'close',
    description: 'Close a door or container.',
    category: 'world',
    roles: ['player', 'mob'],
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
            actor.send("There's no door that way.\r\n");
            return;
        }

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
