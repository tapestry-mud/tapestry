tapestry.commands.register({
    name: 'settrainable',
    description: 'Admin: toggle a stat in or out of the trainable list.',
    category: 'admin',
    admin: true,
    args: {
        entity: { type: 'keyword', required: true },
        ability: { type: 'keyword', required: true },
        flag: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {

        var entityName = resolved.entity;
        var abilityId = resolved.ability.toLowerCase();
        var flagStr = resolved.flag.toLowerCase();

        if (flagStr !== 'true' && flagStr !== 'false') {
            actor.send('Flag must be true or false.\r\n');
            return;
        }

        var enabled = flagStr === 'true';

        tapestry.training.setTrainable(abilityId, enabled);
        actor.send((enabled ? 'Enabled' : 'Disabled') + ' training for ' + abilityId + ' (scope: ' + entityName + ').\r\n');
    }
});
