tapestry.commands.register({
    name: 'recall',
    description: 'Teleport to the recall room.',
    category: 'world',
    roles: ['player'],
    args: {},
    handler: function(actor, resolved) {
        var moved = tapestry.world.teleportEntity(actor.entityId, 'tapestry-core:recall');
        if (moved) {
            actor.send('You are surrounded by a brief flash of light...\r\n');
            tapestry.world.sendRoomDescription(actor.entityId);
            tapestry.events.publish('player.teleported', {
                entityId: actor.entityId
            });
        } else {
            actor.send('You failed to recall.\r\n');
        }
    }
});
