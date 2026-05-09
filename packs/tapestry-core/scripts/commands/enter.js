tapestry.commands.register({
    name: 'enter',
    description: 'Enter a portal or special exit.',
    category: 'world',
    roles: ['player'],
    args: {
        portal: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var keyword = resolved.portal.toLowerCase();
        var roomId = actor.roomId;

        if (!roomId) {
            actor.send("You aren't anywhere.\r\n");
            return;
        }

        var exits = tapestry.portals.getKeywordExits(roomId);
        var match = null;

        for (var i = 0; i < exits.length; i++) {
            if (exits[i].keyword.toLowerCase() === keyword) {
                match = exits[i];
                break;
            }
        }

        if (!match) {
            actor.send("You don't see that here.\r\n");
            return;
        }

        if (match.door) {
            if (match.door.isLocked) {
                actor.send('That is locked.\r\n');
                return;
            }
            if (match.door.isClosed) {
                actor.send('That is closed.\r\n');
                return;
            }
        }

        tapestry.world.sendToRoomExcept(roomId, actor.entityId,
            actor.name + ' passes through the ' + match.name + '.\r\n');

        tapestry.world.teleportEntity(actor.entityId, match.targetRoomId);
        tapestry.world.sendRoomDescription(actor.entityId);
    }
});
