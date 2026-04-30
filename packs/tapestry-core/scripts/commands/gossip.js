tapestry.commands.register({
    name: 'gossip',
    description: 'Send a message to all players',
    category: 'communication',
    handler: function(player, args) {
        var message = args.join(' ');
        if (!message) {
            player.send('Gossip what?\r\n');
            return;
        }

        if (tapestry.world.getProperty(player.entityId, 'nochannels')) {
            player.send('You cannot use channels right now.\r\n');
            return;
        }

        player.send('<gossip>You gossip: "' + message + '"</gossip>\r\n');
        tapestry.world.sendToAll(
            '<gossip>' + player.name + ' gossips: "' + message + '"</gossip>\r\n',
            player.entityId
        );

        var allPlayers = tapestry.world.getOnlinePlayers();
        for (var i = 0; i < allPlayers.length; i++) {
            tapestry.gmcp.send(allPlayers[i].id, 'Comm.Channel', { channel: 'gossip', sender: player.name, text: message });
        }
    }
});
