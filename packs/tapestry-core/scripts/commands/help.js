tapestry.commands.register({
    name: 'help',
    aliases: ['?'],
    description: 'Browse or search help topics.',
    priority: 100,
    handler: function(player, args) {
        tapestry.respond.suppress(player.entityId);

        var term = args ? String(args).trim() : '';

        if (!term) {
            var cats = tapestry.help.categories(player.entityId);

            if (cats.length === 0) {
                player.send('No help topics available.\r\n');
                return;
            }

            var lines = ['Help Topics:\r\n'];
            for (var i = 0; i < cats.length; i++) {
                var cat = cats[i];
                var topics = tapestry.help.list(player.entityId, cat);
                lines.push('  ' + cat + ' (' + topics.length + (topics.length === 1 ? ' topic' : ' topics') + ')\r\n');
            }
            lines.push('\r\nType help [topic] for details.\r\n');
            player.send(lines.join(''));
            return;
        }

        var result = tapestry.help.query(player.entityId, term);

        if (result.status === 'ok') {
            player.send(tapestry.ui.help(result));
            tapestry.gmcp.send(player.entityId, 'Response.Help', {
                status: 'ok',
                topic: result.topic
            });
        } else if (result.status === 'multiple') {
            player.send(tapestry.ui.help(result));
            tapestry.gmcp.send(player.entityId, 'Response.Help', {
                status: 'multiple',
                term: result.term,
                matches: result.matches
            });
        } else {
            player.send(tapestry.ui.help(result));
            tapestry.gmcp.send(player.entityId, 'Response.Help', {
                status: 'no_match',
                term: result.term
            });
        }
    }
});
