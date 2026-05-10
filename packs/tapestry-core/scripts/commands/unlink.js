tapestry.commands.register({
    name: 'unlink',
    aliases: [],
    description: 'Remove a connection from this room.',
    category: 'admin',
    roles: ['player'],
    args: {},
    handler: function(actor, resolved) {
        if (!actor.hasRole('admin')) {
            actor.send('Huh?\r\n');
            return;
        }

        actor.send("Starting unlink wizard. Type 'cancel' or 'quit' to exit at any time.\r\n");
        tapestry.flows.trigger(actor.entityId, "admin_unlink");
    }
});
