tapestry.commands.register({
    name: 'sleep',
    description: 'Lie down and sleep.',
    category: 'world',
    roles: ['player', 'mob'],
    args: {},
    handler: function(actor, resolved) {
        var currentState = tapestry.rest.getRestState(actor.entityId);
        if (currentState === 'sleeping') {
            actor.send('You are already sleeping.\r\n');
            return;
        }
        if (tapestry.combat.isInCombat(actor.entityId)) {
            actor.send("You can't sleep while fighting!\r\n");
            return;
        }

        var result = tapestry.rest.setRestState(actor.entityId, 'sleeping', null);
        if (result && result.success) {
            actor.send('You lie down and sleep.\r\n');
            actor.sendToRoom(actor.name + ' lies down and sleeps.\r\n');
        } else {
            actor.send("You can't sleep right now.\r\n");
        }
    }
});
