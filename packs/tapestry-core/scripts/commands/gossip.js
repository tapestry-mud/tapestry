tapestry.commands.register({
    name: 'gossip',
    description: 'Send a message to all players.',
    category: 'social',
    roles: ['player', 'mob'],
    args: {
        message: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        var message = resolved.message;

        if (tapestry.world.getProperty(actor.entityId, 'nochannels')) {
            actor.send('You cannot use channels right now.\r\n');
            return;
        }

        actor.send('<gossip>You gossip: "' + message + '"</gossip>\r\n');
        tapestry.world.sendToAll(
            '<gossip>' + actor.name + ' gossips: "' + message + '"</gossip>\r\n',
            actor.entityId
        );

        var allPlayers = tapestry.world.getOnlinePlayers();
        for (var i = 0; i < allPlayers.length; i++) {
            tapestry.gmcp.send(allPlayers[i].id, 'Comm.Channel', { channel: 'gossip', sender: actor.name, text: message });
        }
    }
});
