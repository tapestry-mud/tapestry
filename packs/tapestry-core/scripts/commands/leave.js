tapestry.commands.register({
    name: 'leave',
    description: 'Leave a portal or special exit.',
    priority: 0,
    handler: function(player, args) {
        if (!tapestry.returnaddress.has(player.entityId)) {
            player.send('You have nowhere to return to.\r\n');
            return;
        }

        var fromRoomId = tapestry.world.getEntityRoomId(player.entityId);
        var returnRoomId = tapestry.returnaddress.get(player.entityId);

        // Teleport before clearing — return address stays valid during the move event chain
        tapestry.world.teleportEntity(player.entityId, returnRoomId);
        tapestry.returnaddress.clear(player.entityId);
        tapestry.world.sendRoomDescription(player.entityId);

        tapestry.events.publish('return.used', {
            entityId: player.entityId,
            fromRoomId: fromRoomId,
            toRoomId: returnRoomId
        });
    }
});
