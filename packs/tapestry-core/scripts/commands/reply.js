tapestry.commands.register({
    name: 'reply',
    aliases: ['r'],
    description: 'Reply to the last player who sent you a tell.',
    category: 'social',
    roles: ['player'],
    args: {
        message: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        var message = resolved.message;

        var lastFrom = tapestry.world.getProperty(actor.entityId, 'lastTellFrom');
        if (!lastFrom) {
            actor.send('You have no one to reply to.\r\n');
            return;
        }

        var players = tapestry.world.getOnlinePlayers();
        var found = null;
        for (var i = 0; i < players.length; i++) {
            if (players[i].id === lastFrom) {
                found = players[i];
                break;
            }
        }

        if (!found) {
            actor.send('That player is no longer online.\r\n');
            return;
        }

        if (tapestry.world.getProperty(actor.entityId, 'notell')) {
            actor.send('You cannot send tells right now.\r\n');
            return;
        }

        actor.send('<tell>You tell ' + found.name + ': "' + message + '"</tell>\r\n');
        tapestry.world.send(found.id, '<tell>' + actor.name + ' tells you: "' + message + '"</tell>\r\n');
        tapestry.gmcp.send(found.id, 'Comm.Channel', { channel: 'tell', sender: actor.name, text: message });

        tapestry.world.setProperty(found.id, 'lastTellFrom', actor.entityId);
        tapestry.world.setProperty(actor.entityId, 'lastTellTo', found.id);
    }
});
