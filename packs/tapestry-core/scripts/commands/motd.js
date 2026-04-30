tapestry.commands.register({
    name: 'motd',
    description: 'Display the message of the day.',
    priority: 0,
    handler: function(player, args) {
        tapestry.world.sendMotd(player.entityId);
    }
});
