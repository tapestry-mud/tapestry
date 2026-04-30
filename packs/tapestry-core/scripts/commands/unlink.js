tapestry.commands.register({
    name: 'unlink',
    aliases: [],
    description: 'Remove a connection from this room.',
    priority: 10,
    handler: function(player, args) {
        player.send("Starting unlink wizard. Type 'cancel' or 'quit' to exit at any time.\r\n");
        tapestry.flows.trigger(player.entityId, "admin_unlink");
    }
});
