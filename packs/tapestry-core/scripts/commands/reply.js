tapestry.commands.register({
    name: 'reply',
    aliases: ['r'],
    description: 'Reply to the last player who sent you a tell',
    category: 'communication',
    handler: function(player, args) {
        if (args.length < 1) {
            player.send('Reply what?\r\n');
            return;
        }

        var lastFrom = tapestry.world.getProperty(player.entityId, 'lastTellFrom');
        if (!lastFrom) {
            player.send('You have no one to reply to.\r\n');
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
            player.send('That player is no longer online.\r\n');
            return;
        }

        if (tapestry.world.getProperty(player.entityId, 'notell')) {
            player.send('You cannot send tells right now.\r\n');
            return;
        }

        var message = args.join(' ');
        player.send('<tell>You tell ' + found.name + ': "' + message + '"</tell>\r\n');
        tapestry.world.send(found.id, '<tell>' + player.name + ' tells you: "' + message + '"</tell>\r\n');
        tapestry.gmcp.send(found.id, 'Comm.Channel', { channel: 'tell', sender: player.name, text: message });

        tapestry.world.setProperty(found.id, 'lastTellFrom', player.entityId);
        tapestry.world.setProperty(player.entityId, 'lastTellTo', found.id);
    }
});
