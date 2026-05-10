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

function formatDuration(isoString) {
    var then = new Date(isoString);
    var now = new Date();
    var secs = Math.floor((now.getTime() - then.getTime()) / 1000);
    if (secs < 60) { return secs + 's'; }
    var mins = Math.floor(secs / 60);
    if (mins < 60) { return mins + 'm'; }
    var hours = Math.floor(mins / 60);
    mins = mins % 60;
    return hours + 'h' + (mins > 0 ? mins + 'm' : '');
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

        var rows = [{ type: 'empty' }];

        for (var i = 0; i < players.length; i++) {
            var p = players[i];
            var level = getHighestLevel(p.id);
            var race = capitalize(stripPack(p.race));
            var cls = capitalize(stripPack(p.charClass));
            var badge = '';
            if (p.roles && p.roles.indexOf('admin') >= 0) {
                badge = ' <subtle>[Admin]</subtle>';
            }

            var nameCol = '  ' + p.name + badge;
            var infoCol = race + ' ' + cls;
            var levelCol = 'Lv ' + level;

            if (isAdmin) {
                var idle = formatIdleTicks(currentTick, p.lastInputTick);
                var connTime = formatDuration(p.connectedAt);
                var connPrefix = p.connectionId.substring(0, 8);
                var ip = tapestry.world.getProperty(p.id, 'last_ip') || '?';
                var roomName = tapestry.world.getRoomName(p.roomId) || p.roomId || '?';

                rows.push({
                    type: 'cell', cells: [
                        { content: nameCol, width: 22 },
                        { content: infoCol, width: 20 },
                        { content: levelCol, width: 6 },
                        { content: idle, width: 5, align: 'right' },
                        { content: connTime, width: 6, align: 'right' },
                        { content: connPrefix, width: 10 },
                        { content: ip, width: 'fill' }
                    ]
                });
                rows.push({
                    type: 'cell', cells: [
                        { content: '    <subtle>' + roomName + '</subtle>', width: 'fill' }
                    ]
                });
            } else {
                rows.push({
                    type: 'cell', cells: [
                        { content: nameCol, width: 28 },
                        { content: infoCol, width: 22 },
                        { content: levelCol, width: 'fill' }
                    ]
                });
            }
        }

        rows.push({ type: 'empty' });

        var headerRight = players.length + ' online';
        var sections = [
            { rows: [{ type: 'title', left: 'Players Online', right: headerRight }] },
            { separatorAbove: 'minor', rows: rows }
        ];

        if (isAdmin) {
            sections[0].rows.push({
                type: 'cell', cells: [
                    { content: '  <subtle>Name</subtle>', width: 22 },
                    { content: '<subtle>Race/Class</subtle>', width: 20 },
                    { content: '<subtle>Lv</subtle>', width: 6 },
                    { content: '<subtle>Idle</subtle>', width: 5, align: 'right' },
                    { content: '<subtle>Conn</subtle>', width: 6, align: 'right' },
                    { content: '<subtle>Session</subtle>', width: 10 },
                    { content: '<subtle>IP</subtle>', width: 'fill' }
                ]
            });
        }

        var output = tapestry.ui.panel({ sections: sections });
        actor.send('\r\n' + output + '\r\n');
    }
});
