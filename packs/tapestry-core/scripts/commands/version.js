tapestry.commands.register({
    name: 'version',
    aliases: ['ver'],
    description: 'Show engine and pack version info.',
    category: 'info',
    roles: ['player'],
    priority: 0,
    args: {},
    handler: function(actor, resolved) {
        var info = tapestry.world.buildInfo();
        var shortSha = info.engineSha.length > 7 ? info.engineSha.substring(0, 7) : info.engineSha;
        var engineVer = info.engineVersion || 'dev';
        var packs = tapestry.packs.list();

        var packRef = info.packBuildRef || 'dev';
        var rows = [];
        for (var i = 0; i < packs.length; i++) {
            var ver = packs[i].version || 'unknown';
            rows.push({
                type: 'cell',
                cells: [
                    { content: '  ' + packs[i].name, width: 26 },
                    { content: ver + '  (' + packRef + ')', width: 'fill' }
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
                            { content: '  Engine', width: 26 },
                            { content: engineVer + '  (' + shortSha + ')', width: 'fill' }
                        ]
                    }
                ]
            },
            { separatorAbove: 'minor', rows: rows }
        ];

        actor.send('\r\n' + tapestry.ui.panel({ sections: sections }) + '\r\n');
    }
});
