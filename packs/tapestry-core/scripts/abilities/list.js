tapestry.commands.register({
    name: 'skills',
    description: 'List your learned skills.',
    handler: function(player, args) {
        var learned = tapestry.abilities.getLearnedAbilities(player.entityId);
        var skills = [];

        for (var i = 0; i < learned.length; i++) {
            var def = tapestry.abilities.getDefinition(learned[i].id);
            if (def && def.category === 'skill') {
                skills.push({ name: def.name, proficiency: learned[i].proficiency });
            }
        }

        if (skills.length === 0) {
            player.send("You don't know any skills.\r\n");
            return;
        }

        var rows = [{ type: 'title', left: 'Your Skills' }];
        for (var j = 0; j < skills.length; j++) {
            rows.push({
                type: 'cell',
                cells: [
                    { content: '  ' + skills[j].name, width: 'fill' },
                    { type: 'progress', value: skills[j].proficiency, max: 100, width: 20 },
                    { content: skills[j].proficiency + '%', width: 6, align: 'right' }
                ]
            });
        }

        var output = tapestry.ui.panel({ sections: [{ rows: rows }] });
        player.send('\r\n' + output + '\r\n');
    }
});

tapestry.commands.register({
    name: 'spells',
    description: 'List your learned spells.',
    handler: function(player, args) {
        var learned = tapestry.abilities.getLearnedAbilities(player.entityId);
        var spells = [];

        for (var i = 0; i < learned.length; i++) {
            var def = tapestry.abilities.getDefinition(learned[i].id);
            if (def && def.category === 'spell') {
                spells.push({ name: def.name, proficiency: learned[i].proficiency });
            }
        }

        if (spells.length === 0) {
            player.send("You don't know any spells.\r\n");
            return;
        }

        var rows = [{ type: 'title', left: 'Your Spells' }];
        for (var j = 0; j < spells.length; j++) {
            rows.push({
                type: 'cell',
                cells: [
                    { content: '  ' + spells[j].name, width: 'fill' },
                    { type: 'progress', value: spells[j].proficiency, max: 100, width: 20 },
                    { content: spells[j].proficiency + '%', width: 6, align: 'right' }
                ]
            });
        }

        var output = tapestry.ui.panel({ sections: [{ rows: rows }] });
        player.send('\r\n' + output + '\r\n');
    }
});
