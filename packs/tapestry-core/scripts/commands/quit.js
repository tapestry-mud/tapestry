tapestry.commands.register({
    name: 'quit',
    aliases: ['qq'],
    description: 'Disconnect from the game.',
    priority: 0,
    handler: function(player, args) {
        player.send('Farewell, adventurer. Until next time.\r\n');
        tapestry.world.sendToRoomExcept(
            player.roomId,
            player.entityId,
            player.name + ' fades from existence.\r\n'
        );
        tapestry.world.disconnectPlayer(player.entityId);
    }
});
