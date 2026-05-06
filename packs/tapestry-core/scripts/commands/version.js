tapestry.commands.register({
    name: 'version',
    aliases: ['ver'],
    description: 'Show engine and pack version info.',
    priority: 0,
    handler: function(player, args) {
        var info = tapestry.world.buildInfo();
        var shortSha = info.engineSha.length > 7 ? info.engineSha.substring(0, 7) : info.engineSha;
        var packs = tapestry.packs.list();

        var rows = [];
        for (var i = 0; i < packs.length; i++) {
            rows.push({
                type: 'cell',
                cells: [
                    { content: '  ' + packs[i].name, width: 26 },
                    { content: packs[i].version || 'unknown', width: 'fill' }
                ]
            });
        }

        var sections = [
            { rows: [{ type: 'title', left: 'Server Version', right: '' }] },
            {
                separatorAbove: 'minor',
                rows: [
                    {
                        type: 'cell',
                        cells: [
                            { content: '  Engine build', width: 26 },
                            { content: shortSha, width: 'fill' }
                        ]
                    }
                ]
            },
            { separatorAbove: 'minor', rows: rows }
        ];

        player.send('\r\n' + tapestry.ui.panel({ sections: sections }) + '\r\n');
    }
});
