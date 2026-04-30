function resolvePlayerByName(name) {
    var players = tapestry.world.getOnlinePlayers();
    var lowerName = name.toLowerCase();
    for (var i = 0; i < players.length; i++) {
        if (players[i].name.toLowerCase() === lowerName) {
            return players[i];
        }
    }
    return null;
}

tapestry.commands.register({
    name: 'learn',
    admin: true,
    description: 'Grant an ability to yourself or another player.',
    priority: 0,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (!args || args.length < 1) {
            player.send('Usage: learn [ability-id] [proficiency] [player]\r\n');
            player.send('  Examples: learn kick, learn fireball 50, learn dodge 75 Travis\r\n\r\n');

            var all = tapestry.abilities.getAll();
            var skills = [];
            var spells = [];
            for (var i = 0; i < all.length; i++) {
                if (all[i].category === 'skill') {
                    skills.push(all[i]);
                } else {
                    spells.push(all[i]);
                }
            }

            if (skills.length > 0) {
                player.send('<heading>Skills:</heading> ');
                var skillNames = [];
                for (var s = 0; s < skills.length; s++) {
                    skillNames.push(skills[s].id + ' <subtle>(' + skills[s].type + ')</subtle>');
                }
                player.send(skillNames.join(', ') + '\r\n');
            }
            if (spells.length > 0) {
                player.send('<heading>Spells:</heading> ');
                var spellNames = [];
                for (var p = 0; p < spells.length; p++) {
                    spellNames.push(spells[p].id + ' <subtle>(' + spells[p].type + ')</subtle>');
                }
                player.send(spellNames.join(', ') + '\r\n');
            }
            return;
        }

        var abilityId = args[0].toLowerCase();
        var proficiency = 1;
        var target = { id: player.entityId, name: player.name };

        if (args.length >= 2) {
            var parsed = parseInt(args[1], 10);
            if (!isNaN(parsed) && parsed > 0) {
                proficiency = parsed;
            }
        }

        if (args.length >= 3) {
            var targetName = args[2];
            if (targetName.toLowerCase() !== 'self' && targetName.toLowerCase() !== player.name.toLowerCase()) {
                var resolved = resolvePlayerByName(targetName);
                if (!resolved) {
                    player.send("Player '" + targetName + "' not found.\r\n");
                    return;
                }
                target = resolved;
            }
        }

        var def = tapestry.abilities.getDefinition(abilityId);
        if (!def) {
            player.send("Unknown ability: " + abilityId + "\r\n");
            return;
        }

        tapestry.abilities.learn(target.id, abilityId, { proficiency: proficiency });
        player.send('Granted ' + def.name + ' to ' + target.name + ' at ' + proficiency + '% proficiency.\r\n');
    }
});
