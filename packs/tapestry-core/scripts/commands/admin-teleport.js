tapestry.commands.register({
    name: 'teleport',
    aliases: ['tp'],
    description: 'Teleport a player to a room.',
    category: 'admin',
    roles: ['player'],
    args: {
        player: { type: 'keyword', required: true },
        roomId: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        if (!actor.hasRole('admin')) {
            actor.send('Huh?\r\n');
            return;
        }

        var playerName = resolved.player;
        var roomId = resolved.roomId;

        var players = tapestry.world.getOnlinePlayers();
        var lowerName = playerName.toLowerCase();
        var targetEntity = null;

        for (var i = 0; i < players.length; i++) {
            if (players[i].name.toLowerCase() === lowerName) {
                targetEntity = players[i];
                break;
            }
        }

        if (!targetEntity) {
            actor.send('Player not found: ' + playerName + '\r\n');
            return;
        }

        var roomName = tapestry.world.getRoomName(roomId);

        if (!roomName) {
            actor.send('Unknown destination: ' + roomId + '\r\n');
            return;
        }

        tapestry.world.teleportEntity(targetEntity.entityId, roomId);
        actor.send('Teleported ' + targetEntity.name + ' to ' + roomName + '.\r\n');
    }
});
