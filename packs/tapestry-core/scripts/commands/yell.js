tapestry.commands.register({
    name: 'yell',
    description: 'Shout a message to the entire area.',
    category: 'social',
    roles: ['player', 'mob'],
    args: {
        message: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        var message = resolved.message;
        var upper = message.toUpperCase();

        actor.send('You yell "<yell>' + upper + '!</yell>"\r\n');
        actor.sendToRoom(actor.name + ' yells "<yell>' + upper + '!</yell>"\r\n');

        var allPlayers = tapestry.world.getOnlinePlayers();
        for (var i = 0; i < allPlayers.length; i++) {
            tapestry.gmcp.send(allPlayers[i].id, 'Comm.Channel', { channel: 'yell', sender: actor.name, text: message });
        }
    }
});
