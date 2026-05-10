tapestry.commands.register({
    name: 'connections',
    aliases: [],
    description: 'List connections for this room or all rooms.',
    category: 'admin',
    admin: true,
    args: {},
    priority: 10,
    handler: function(actor, resolved) {

        if (resolved[0] === 'all') {
            var all = tapestry.connections.getAll();
            if (all.length === 0) {
                actor.send('No connections on this server.\r\n');
                return;
            }
            all.forEach(function(c) {
                actor.send(formatConnection(c) + '\r\n');
            });
        } else {
            var conns = tapestry.connections.getForRoom(actor.roomId);
            if (conns.length === 0) {
                actor.send('No connections for this room.\r\n');
                return;
            }
            actor.send('Connections for ' + actor.roomId + ':\r\n');
            conns.forEach(function(c) {
                actor.send('  ' + formatConnection(c) + '\r\n');
            });
        }
    }
});

function formatConnection(c) {
    var fromLabel = c.from.type === 'direction' ? c.from.direction : 'enter ' + c.from.keyword;
    var toLabel = c.to.type === 'one-way' ? 'one-way' : (c.to.type === 'direction' ? c.to.direction : c.to.keyword + ' back');
    return fromLabel + ' --> ' + c.to.room + ' (' + toLabel + ')';
}
