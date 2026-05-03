tapestry.commands.register({
    name: 'connections',
    aliases: [],
    description: 'List connections for this room or all rooms.',
    priority: 10,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (args[0] === 'all') {
            var all = tapestry.connections.getAll();
            if (all.length === 0) {
                player.send("No connections on this server.\r\n");
                return;
            }
            all.forEach(function(c) { player.send(formatConnection(c) + "\r\n"); });
        } else {
            var conns = tapestry.connections.getForRoom(player.roomId);
            if (conns.length === 0) {
                player.send("No connections for this room.\r\n");
                return;
            }
            player.send("Connections for " + player.roomId + ":\r\n");
            conns.forEach(function(c) { player.send("  " + formatConnection(c) + "\r\n"); });
        }
    }
});

function formatConnection(c) {
    var fromLabel = c.from.type === 'direction' ? c.from.direction : 'enter ' + c.from.keyword;
    var toLabel = c.to.type === 'one-way' ? 'one-way' : (c.to.type === 'direction' ? c.to.direction : c.to.keyword + ' back');
    return fromLabel + " --> " + c.to.room + " (" + toLabel + ")";
}
