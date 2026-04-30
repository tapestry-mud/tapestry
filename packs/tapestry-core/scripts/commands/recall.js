tapestry.commands.register({
    name: 'recall',
    description: 'Teleport to the recall room.',
    priority: 0,
    handler: function(player, args) {
        var moved = tapestry.world.teleportEntity(player.entityId, 'core:recall');
        if (moved) {
            player.send('You are surrounded by a brief flash of light...\r\n');
            tapestry.world.sendRoomDescription(player.entityId);
            tapestry.events.publish('player.teleported', {
                entityId: player.entityId
            });
        } else {
            player.send('You failed to recall.\r\n');
        }
    }
});
