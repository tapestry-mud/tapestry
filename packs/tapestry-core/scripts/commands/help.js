tapestry.commands.register({
    name: 'help',
    aliases: ['?'],
    description: 'Browse or search help topics.',
    category: 'info',
    priority: 100,
    roles: ['player'],
    args: {
        topic: { type: 'keyword', required: false }
    },
    handler: function(actor, resolved) {
        tapestry.respond.suppress(actor.entityId);

        var helpId = actor.isChargen ? null : actor.entityId;
        var term = resolved.topic ? String(resolved.topic).trim() : '';

        if (!term) {
            var cats = helpId ? tapestry.help.categories(helpId) : tapestry.help.categories();

            if (cats.length === 0) {
                actor.send('No help topics available.\r\n');
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
            actor.send(lines.join(''));
            tapestry.gmcp.send(actor.entityId, 'Response.Help', {
                status: 'multiple',
                term: '',
                matches: matches
            });
            return;
        }

        var result = helpId ? tapestry.help.query(helpId, term) : tapestry.help.query(term);

        if (result.status === 'ok') {
            actor.send(tapestry.ui.help(result));
            tapestry.gmcp.send(actor.entityId, 'Response.Help', {
                status: 'ok',
                topic: result.topic
            });
        } else if (result.status === 'multiple') {
            actor.send(tapestry.ui.help(result));
            tapestry.gmcp.send(actor.entityId, 'Response.Help', {
                status: 'multiple',
                term: result.term,
                matches: result.matches
            });
        } else {
            actor.send(tapestry.ui.help(result));
            tapestry.gmcp.send(actor.entityId, 'Response.Help', {
                status: 'no_match',
                term: result.term
            });
        }
    }
});
