function renderTree(player, filter) {
    var classId = tapestry.world.getProperty(player.entityId, 'class') || '';
    if (!classId) {
        player.send("You have no class.\r\n");
        return;
    }

    var classDef = tapestry.classes.get(classId);
    if (!classDef) {
        player.send("You have no class.\r\n");
        return;
    }

    var currentLevel = tapestry.progression.getLevel(player.entityId, classDef.track);
    var path = classDef.path || [];

    var pathIdSet = {};
    for (var i = 0; i < path.length; i++) {
        pathIdSet[path[i].ability_id] = true;
    }

    var learned = [];
    var upcoming = [];
    for (var pi = 0; pi < path.length; pi++) {
        var entry = path[pi];
        var def = tapestry.abilities.getDefinition(entry.ability_id);
        if (!def) { continue; }
        if (filter && def.category !== filter) { continue; }
        var prof = tapestry.abilities.getProficiency(player.entityId, entry.ability_id);
        if (prof && prof > 0) {
            learned.push({ entry: entry, def: def, prof: prof });
        } else {
            upcoming.push({ entry: entry, def: def });
        }
    }

    var allLearned = tapestry.abilities.getLearnedAbilities(player.entityId);
    var alsoLearned = [];
    for (var ai = 0; ai < allLearned.length; ai++) {
        var al = allLearned[ai];
        if (pathIdSet[al.id]) { continue; }
        var alDef = tapestry.abilities.getDefinition(al.id);
        if (!alDef) { continue; }
        if (filter && alDef.category !== filter) { continue; }
        alsoLearned.push({ def: alDef, prof: al.proficiency });
    }

    var sections = [];

    sections.push({
        rows: [{
            type: 'title',
            left: classDef.name + ' Path',
            right: 'Level ' + currentLevel
        }]
    });

    if (learned.length > 0) {
        var learnedRows = [{ type: 'text', content: '  Learned' }];
        for (var li = 0; li < learned.length; li++) {
            var l = learned[li];
            var displayName = l.def.short_name || l.def.name;
            learnedRows.push({
                type: 'cell',
                cells: [
                    { content: '    ' + displayName, width: 24 },
                    { content: 'Lvl ' + l.entry.level, width: 8 },
                    { content: l.prof + '%', width: 'fill' }
                ]
            });
        }
        sections.push({ separatorAbove: 'minor', rows: learnedRows });
    }

    if (upcoming.length > 0) {
        var upcomingRows = [{ type: 'text', content: '  Upcoming' }];
        for (var ui = 0; ui < upcoming.length; ui++) {
            var u = upcoming[ui];
            var uName = u.def.short_name || u.def.name;
            var tag = u.entry.unlocked_via ? ' [' + u.entry.unlocked_via + ']' : '';
            upcomingRows.push({
                type: 'cell',
                cells: [
                    { content: '    ' + uName + tag, width: 24 },
                    { content: 'Lvl ' + u.entry.level, width: 'fill' }
                ]
            });
        }
        sections.push({ separatorAbove: 'minor', rows: upcomingRows });
    }

    if (alsoLearned.length > 0) {
        var alsoRows = [{ type: 'text', content: '  Also learned' }];
        for (var si = 0; si < alsoLearned.length; si++) {
            var a = alsoLearned[si];
            var aName = a.def.short_name || a.def.name;
            alsoRows.push({
                type: 'cell',
                cells: [
                    { content: '    ' + aName, width: 24 },
                    { content: a.prof + '%', width: 'fill' }
                ]
            });
        }
        sections.push({ separatorAbove: 'minor', rows: alsoRows });
    }

    sections.push({
        separatorAbove: 'major',
        rows: [{ type: 'footer', content: 'tree skills / tree spells to filter' }]
    });

    var output = tapestry.ui.panel({ sections: sections });
    player.send('\r\n' + output + '\r\n');
}

tapestry.commands.register({
    name: 'tree',
    description: 'Show your class path and learned abilities.',
    handler: function(player, args) {
        renderTree(player, null);
    }
});

tapestry.commands.register({
    name: 'skills',
    description: 'Show your learned skills.',
    handler: function(player, args) {
        renderTree(player, 'skill');
    }
});

tapestry.commands.register({
    name: 'spells',
    description: 'Show your learned spells.',
    handler: function(player, args) {
        renderTree(player, 'spell');
    }
});
