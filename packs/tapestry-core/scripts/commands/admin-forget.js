tapestry.commands.register({
    name: 'forget',
    description: 'Remove an ability from an entity.',
    category: 'admin',
    admin: true,
    args: {
        entity: { type: 'keyword', required: true },
        ability: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var entityName = resolved.entity;
        var abilityId = resolved.ability;

        var target = null;
        if (entityName.toLowerCase() === 'self' || entityName.toLowerCase() === actor.name.toLowerCase()) {
            target = { id: actor.entityId, name: actor.name };
        } else {
            var players = tapestry.world.getOnlinePlayers();
            var lowerName = entityName.toLowerCase();
            for (var i = 0; i < players.length; i++) {
                if (players[i].name.toLowerCase() === lowerName) {
                    target = { id: players[i].id, name: players[i].name };
                    break;
                }
            }
        }

        if (!target) {
            actor.send("Player '" + entityName + "' not found.\r\n");
            return;
        }

        tapestry.abilities.forget(target.id, abilityId);
        actor.send('Removed ' + abilityId + ' from ' + target.name + '.\r\n');
    }
});
