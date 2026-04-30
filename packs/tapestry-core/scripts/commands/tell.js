tapestry.commands.register({
    name: 'tell',
    aliases: ['t'],
    description: 'Send a private message to a player',
    category: 'communication',
    handler: function(player, args) {
        if (args.length < 2) {
            player.send('Usage: tell [player] [message]\r\n');
            return;
        }

        if (tapestry.world.getProperty(player.entityId, 'notell')) {
            player.send('You cannot send tells right now.\r\n');
            return;
        }

        if (tapestry.world.getProperty(player.entityId, 'nochannels')) {
            player.send('You cannot use channels right now.\r\n');
            return;
        }

        var targetName = args[0];
        var target = tapestry.world.findPlayerByName(targetName);
        if (!target) {
            player.send(targetName + ' is not online.\r\n');
            return;
        }

        if (tapestry.world.getProperty(target.id, 'notell')) {
            player.send(target.name + ' is not accepting tells right now.\r\n');
            return;
        }

        var message = args.slice(1).join(' ');
        player.send('<tell>You tell ' + target.name + ': "' + message + '"</tell>\r\n');
        tapestry.world.send(target.id, '<tell>' + player.name + ' tells you: "' + message + '"</tell>\r\n');
        tapestry.gmcp.send(target.id, 'Comm.Channel', { channel: 'tell', sender: player.name, text: message });

        tapestry.world.setProperty(target.id, 'lastTellFrom', player.entityId);
        tapestry.world.setProperty(player.entityId, 'lastTellTo', target.id);
    }
});
