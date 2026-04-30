tapestry.commands.register({
    name: 'commands',
    aliases: ['cmds'],
    description: 'List commands available to you.',
    handler: function(player, args) {
        var filter = args.length > 0 ? args[0].toLowerCase() : null;
        var entries = tapestry.commands.listForPlayer(player.entityId);

        var grouped = {};
        for (var i = 0; i < entries.length; i++) {
            var e = entries[i];
            if (filter && e.category.toLowerCase() !== filter) { continue; }
            if (!grouped[e.category]) { grouped[e.category] = []; }
            grouped[e.category].push(e);
        }

        var categories = Object.keys(grouped).sort();
        if (categories.length === 0) {
            player.send('No commands match.\r\n');
            return;
        }

        var sections = [];
        for (var c = 0; c < categories.length; c++) {
            sections.push(buildCategorySection(categories[c], grouped[categories[c]]));
        }

        var output = tapestry.ui.panel({ sections: sections });
        player.send('\r\n' + output + '\r\n');
    }
});

function buildCategorySection(category, entries) {
    entries.sort(function(a, b) { return a.keyword.localeCompare(b.keyword); });

    var label = category.charAt(0).toUpperCase() + category.slice(1);
    var titleRow = category === 'admin'
        ? { type: 'title', left: label, right: 'admins only' }
        : { type: 'title', left: label };

    var rows = [titleRow];

    var described = [];
    var undescribed = [];
    for (var i = 0; i < entries.length; i++) {
        if (entries[i].description) {
            described.push(entries[i]);
        } else {
            undescribed.push(entries[i]);
        }
    }

    for (var d = 0; d < described.length; d++) {
        rows.push({
            type: 'cell',
            cells: [
                { content: '  ' + described[d].keyword, width: 18 },
                { content: described[d].description, width: 'fill' }
            ]
        });
    }

    if (undescribed.length > 0) {
        var names = [];
        for (var u = 0; u < undescribed.length; u++) {
            names.push(undescribed[u].keyword);
        }
        rows.push({ type: 'text', content: '  ' + names.join(', ') });
    }

    return { separatorAbove: 'minor', rows: rows };
}
