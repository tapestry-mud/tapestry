tapestry.commands.register({
    name: 'unlink',
    aliases: [],
    description: 'Remove a connection from this room.',
    priority: 10,
    handler: function(player, args) {
        tapestry.flows.trigger(player.entityId, "admin_unlink");
    }
});
