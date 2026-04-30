tapestry.commands.register({
    name: 'wake',
    aliases: ['stand'],
    description: 'Wake up and stand.',
    category: 'rest',
    handler: function(player, args) {
        var currentState = tapestry.rest.getRestState(player.entityId);
        if (currentState === 'awake') {
            player.send('You are already standing.\r\n');
            return;
        }
        var result = tapestry.rest.setRestState(player.entityId, 'awake');
        if (result && result.success) {
            player.send('You wake and stand up.\r\n');
            player.sendToRoom(player.name + ' wakes up and stands.\r\n');
        } else {
            player.send("You can't do that right now.\r\n");
        }
    }
});
