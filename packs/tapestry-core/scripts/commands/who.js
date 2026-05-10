function stripPack(id) {
    if (!id) { return ''; }
    var idx = id.indexOf(':');
    return idx >= 0 ? id.substring(idx + 1) : id;
}

function capitalize(s) {
    if (!s) { return ''; }
    return s.charAt(0).toUpperCase() + s.substring(1);
}

function getHighestLevel(entityId) {
    var tracks = tapestry.progression.getTracks();
    var highest = 0;
    for (var i = 0; i < tracks.length; i++) {
        var info = tapestry.progression.getInfo(entityId, tracks[i].name);
        if (info && info.level > highest) {
            highest = info.level;
        }
    }
    return highest;
}

function formatIdleTicks(currentTick, lastInputTick) {
    var idleTicks = currentTick - lastInputTick;
    var secs = Math.floor(idleTicks / 10);
    if (secs < 10) { return ''; }
    if (secs < 60) { return secs + 's'; }
    var mins = Math.floor(secs / 60);
    if (mins < 60) { return mins + 'm'; }
    return Math.floor(mins / 60) + 'h';
}

function getRoleBadge(roles) {
    if (!roles) { return ''; }
    if (roles.indexOf('admin') >= 0) { return '<subtle>[Admin]</subtle>'; }
    if (roles.indexOf('builder') >= 0) { return '<subtle>[Builder]</subtle>'; }
    return '';
}

tapestry.commands.register({
    name: 'who',
    description: 'List players currently online.',
    category: 'info',
    roles: ['player'],
    args: {},
    priority: 0,
    handler: function(actor, resolved) {
        var players = tapestry.world.getOnlinePlayers();
        var isAdmin = actor.hasRole('admin');
        var currentTick = tapestry.world.getCurrentTick();

        var headerCells = [
            { content: '', width: 11 },
            { content: '<subtle>Name</subtle>', width: 14 },
            { content: '<subtle>Lv</subtle>', width: 7 },
            { content: '<subtle>Race/Class</subtle>', width: 22 }
        ];
        if (isAdmin) {
            headerCells.push({ content: '<subtle>IP</subtle>', width: 17 });
        }
        headerCells.push({ content: '<subtle>Idle</subtle> ', width: 'fill', align: 'right' });
        var headerRows = [{ type: 'cell', cells: headerCells }];

        var rows = [{ type: 'empty' }];

        for (var i = 0; i < players.length; i++) {
            var p = players[i];
            var level = getHighestLevel(p.id);
            var race = capitalize(stripPack(p.race));
            var cls = capitalize(stripPack(p.charClass));
            var badge = getRoleBadge(p.roles);
            var idle = formatIdleTicks(currentTick, p.lastInputTick);
            var idleDisplay = idle ? idle + ' ' : '';

            var rowCells = [
                { content: badge, width: 11, align: 'right' },
                { content: ' ' + p.name, width: 14 },
                { content: 'Lv ' + level, width: 7 },
                { content: race + ' ' + cls, width: 22 }
            ];
            if (isAdmin) {
                var ip = tapestry.world.getProperty(p.id, 'last_ip') || '';
                rowCells.push({ content: '<subtle>' + ip + '</subtle>', width: 17 });
            }
            rowCells.push({ content: idleDisplay, width: 'fill', align: 'right' });
            rows.push({ type: 'cell', cells: rowCells });
        }

        rows.push({ type: 'empty' });

        var output = tapestry.ui.panel({
            sections: [
                { rows: headerRows },
                { separatorAbove: 'minor', rows: rows },
                { separatorAbove: 'minor', rows: [
                    { type: 'footer', content: 'Players Online: ' + players.length }
                ]}
            ]
        });
        actor.send('\r\n' + output + '\r\n');
    }
});
