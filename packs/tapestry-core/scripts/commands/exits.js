tapestry.commands.register({
    name: 'exits',
    description: 'List available exits from the current room.',
    priority: 0,
    handler: function(player, args) {
        var exits = tapestry.world.getRoomExits(player.entityId);
        if (exits.length === 0) {
            player.send('There are no obvious exits.\r\n');
        } else {
            player.send('<direction>Obvious exits: ' + exits.join(', ') + '</direction>\r\n');
        }
    }
});
