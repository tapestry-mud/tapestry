tapestry.commands.register({
    name: 'help',
    aliases: ['?'],
    description: 'Browse or search help topics.',
    priority: 100,
    handler: function(player, args) {
        tapestry.respond.suppress(player.entityId);

        var helpId = player.isChargen ? null : player.entityId;
        var term = args ? String(args).trim() : '';

        if (!term) {
            var cats = helpId ? tapestry.help.categories(helpId) : tapestry.help.categories();

            if (cats.length === 0) {
                player.send('No help topics available.\r\n');
                return;
            }

            var lines = ['Help Topics:\r\n'];
            var matches = [];
            for (var i = 0; i < cats.length; i++) {
                var cat = String(cats[i]);
                var topics = helpId ? tapestry.help.list(helpId, cat) : tapestry.help.list(cat);
                var count = topics.length;
                lines.push('  ' + cat + ' (' + count + (count === 1 ? ' topic' : ' topics') + ')\r\n');
                matches.push({
                    id: cat,
                    title: cat.charAt(0).toUpperCase() + cat.slice(1),
                    brief: count + (count === 1 ? ' topic' : ' topics')
                });
            }
            lines.push('\r\nType help [topic] for details.\r\n');
            player.send(lines.join(''));
            tapestry.gmcp.send(player.entityId, 'Response.Help', {
                status: 'multiple',
                term: '',
                matches: matches
            });
            return;
        }

        var result = helpId ? tapestry.help.query(helpId, term) : tapestry.help.query(term);

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
