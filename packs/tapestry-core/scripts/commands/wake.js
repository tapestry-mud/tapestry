tapestry.commands.register({
    name: 'wake',
    aliases: ['stand'],
    description: 'Wake up and stand.',
    category: 'world',
    roles: ['player', 'mob'],
    args: {},
    handler: function(actor, resolved) {
        var currentState = tapestry.rest.getRestState(actor.entityId);
        if (currentState === 'awake') {
            actor.send('You are already standing.\r\n');
            return;
        }
        var result = tapestry.rest.setRestState(actor.entityId, 'awake');
        if (result && result.success) {
            actor.send('You wake and stand up.\r\n');
            actor.sendToRoom(actor.name + ' wakes up and stands.\r\n');
        } else {
            actor.send("You can't do that right now.\r\n");
        }
    }
});
