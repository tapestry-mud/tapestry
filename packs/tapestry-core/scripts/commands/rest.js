tapestry.commands.register({
    name: 'rest',
    description: 'Sit down and rest.',
    category: 'world',
    roles: ['player', 'mob'],
    args: {},
    handler: function(actor, resolved) {
        var currentState = tapestry.rest.getRestState(actor.entityId);
        if (currentState === 'resting' || currentState === 'sleeping') {
            actor.send('You are already resting.\r\n');
            return;
        }
        if (tapestry.combat.isInCombat(actor.entityId)) {
            actor.send("You can't rest while fighting!\r\n");
            return;
        }

        var result = tapestry.rest.setRestState(actor.entityId, 'resting', null);
        if (result && result.success) {
            actor.send('You sit down and rest.\r\n');
            actor.sendToRoom(actor.name + ' sits down and rests.\r\n');
        } else {
            actor.send("You can't rest right now.\r\n");
        }
    }
});
