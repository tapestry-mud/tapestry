// Example command: registers "hello" with an alias "hi".
// Try: hello, hello world, hi
tapestry.commands.register({
    name: 'hello',
    aliases: ['hi'],
    description: 'Greet the world',
    handler: function(player, args) {
        var target = args.trim() || 'world';
        player.send('Hello, ' + target + '!\r\n');
        tapestry.world.sendToRoomExcept(
            player.roomId,
            player.entityId,
            player.name + ' says hello to ' + target + '.\r\n'
        );
    }
});
