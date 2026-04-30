tapestry.commands.register({
    name: 'yell',
    description: 'Shout a message to the entire area.',
    priority: 0,
    handler: function(player, args) {
        var message = args.join(' ');
        if (!message) {
            player.send('Yell what?\r\n');
            return;
        }
        player.send('You yell "<yell>' + message.toUpperCase() + '!</yell>"\r\n');
        tapestry.world.sendToAll(
            player.name + ' yells "<yell>' + message.toUpperCase() + '!</yell>"\r\n',
            player.entityId
        );

        var allPlayers = tapestry.world.getOnlinePlayers();
        for (var i = 0; i < allPlayers.length; i++) {
            tapestry.gmcp.send(allPlayers[i].id, 'Comm.Channel', { channel: 'yell', sender: player.name, text: message });
        }
    }
});
