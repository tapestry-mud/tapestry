tapestry.commands.register({
    name: 'leave',
    description: 'Leave a portal or special exit.',
    category: 'world',
    roles: ['player'],
    args: {},
    handler: function(actor, resolved) {
        if (!tapestry.returnaddress.has(actor.entityId)) {
            actor.send('You have nowhere to return to.\r\n');
            return;
        }

        var fromRoomId = tapestry.world.getEntityRoomId(actor.entityId);
        var returnRoomId = tapestry.returnaddress.get(actor.entityId);

        // Teleport before clearing -- return address stays valid during the move event chain
        tapestry.world.teleportEntity(actor.entityId, returnRoomId);
        tapestry.returnaddress.clear(actor.entityId);
        tapestry.world.sendRoomDescription(actor.entityId);

        tapestry.events.publish('return.used', {
            entityId: actor.entityId,
            fromRoomId: fromRoomId,
            toRoomId: returnRoomId
        });
    }
});
