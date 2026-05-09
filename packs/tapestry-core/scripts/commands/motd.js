tapestry.commands.register({
    name: 'motd',
    description: 'Display the message of the day.',
    category: 'info',
    roles: ['player'],
    args: {},
    priority: 0,
    handler: function(actor, resolved) {
        tapestry.world.sendMotd(actor.entityId);
    }
});
