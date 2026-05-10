tapestry.commands.register({
    name: 'learn',
    description: 'Grant an ability to a player at a specified proficiency.',
    category: 'admin',
    admin: true,
    args: {
        entity: { type: 'keyword', required: true },
        ability: { type: 'keyword', required: true },
        proficiency: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var entityName = resolved.entity;
        var abilityId = resolved.ability.toLowerCase();
        var proficiency = parseInt(resolved.proficiency, 10);

        if (isNaN(proficiency) || proficiency < 1) {
            actor.send('Proficiency must be a positive number.\r\n');
            return;
        }

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

        var def = tapestry.abilities.getDefinition(abilityId);
        if (!def) {
            actor.send('Unknown ability: ' + abilityId + '\r\n');
            return;
        }

        tapestry.abilities.learn(target.id, abilityId, { proficiency: proficiency });
        actor.send('Granted ' + def.name + ' to ' + target.name + ' at ' + proficiency + '% proficiency.\r\n');
    }
});
