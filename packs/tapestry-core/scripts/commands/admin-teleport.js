function resolvePlayerByName(name) {
    var players = tapestry.world.getOnlinePlayers();
    var lowerName = name.toLowerCase();
    for (var i = 0; i < players.length; i++) {
        if (players[i].name.toLowerCase() === lowerName) {
            return players[i];
        }
    }
    return null;
}

tapestry.commands.register({
    name: 'teleport',
    aliases: ['tp'],
    admin: true,
    description: 'Teleport yourself or another player to a room or player.',
    priority: 0,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (args.length === 0) {
            player.send('Usage: teleport [room-id] | teleport [player] [room-id] | teleport [player] [player]\r\n');
            return;
        }

        if (args.length === 1) {
            var roomName = tapestry.world.getRoomName(args[0]);
            if (roomName) {
                tapestry.world.teleportEntity(player.entityId, args[0]);
                tapestry.world.sendRoomDescription(player.entityId);
                player.send('Teleported to ' + roomName + '.\r\n');
            } else {
                player.send('Unknown destination: ' + args[0] + '\r\n');
            }
            return;
        }

        var targetEntity = resolvePlayerByName(args[0]);
        if (!targetEntity) {
            player.send('Player not found: ' + args[0] + '\r\n');
            return;
        }

        var destPlayer = resolvePlayerByName(args[1]);
        if (destPlayer) {
            var destRoomId = tapestry.world.getEntityRoomId(destPlayer.id);
            if (destRoomId) {
                tapestry.world.teleportEntity(targetEntity.id, destRoomId);
                var destRoomName = tapestry.world.getRoomName(destRoomId);
                player.send('Teleported ' + targetEntity.name + ' to ' + destRoomName + '.\r\n');
            } else {
                player.send(destPlayer.name + ' is not in a room.\r\n');
            }
        } else {
            var roomName = tapestry.world.getRoomName(args[1]);
            if (roomName) {
                tapestry.world.teleportEntity(targetEntity.id, args[1]);
                player.send('Teleported ' + targetEntity.name + ' to ' + roomName + '.\r\n');
            } else {
                player.send('Unknown destination: ' + args[1] + '\r\n');
            }
        }
    }
});
