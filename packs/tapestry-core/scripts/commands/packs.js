tapestry.commands.register({
    name: 'packs',
    description: 'Show loaded content packs and credits',
    handler: function(player, args) {
        var loaded = tapestry.packs.list();

        var rows = [{ type: 'title', left: 'Loaded Content Packs' }];

        for (var i = 0; i < loaded.length; i++) {
            var pack = loaded[i];
            var name = (pack.displayName || pack.name) + ' v' + pack.version;
            var credit = pack.author || '';

            rows.push({
                type: 'cell',
                cells: [
                    { content: '  ' + name, width: 36 },
                    { content: credit, width: 'fill', align: 'right' }
                ]
            });

            if (pack.description) {
                rows.push({ type: 'text', content: '    ' + pack.description });
            }

            if (pack.copyright) {
                rows.push({ type: 'text', content: '    ' + pack.copyright });
            }
        }

        var output = tapestry.ui.panel({ sections: [{ rows: rows }] });
        player.send('\r\n' + output + '\r\n');
    }
});
