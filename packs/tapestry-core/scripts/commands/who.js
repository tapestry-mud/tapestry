tapestry.commands.register({
    name: 'who',
    description: 'List players currently online.',
    category: 'info',
    roles: ['player'],
    args: {},
    priority: 0,
    handler: function(actor, resolved) {
        var players = tapestry.world.getOnlinePlayers();
        var contentRows = [{ type: 'empty' }];
        players.forEach(function(p) {
            contentRows.push({ type: 'text', content: '  ' + p.name });
        });
        contentRows.push({ type: 'empty' });
        var output = tapestry.ui.panel({
            sections: [
                { rows: [{ type: 'title', left: 'Players Online', right: players.length + ' online' }] },
                { separatorAbove: 'minor', rows: contentRows }
            ]
        });
        actor.send('\r\n' + output + '\r\n');
    }
});
