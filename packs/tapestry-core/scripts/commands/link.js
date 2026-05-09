tapestry.commands.register({
    name: 'link',
    aliases: [],
    description: 'Link rooms across packs via guided flow.',
    category: 'admin',
    roles: ['player'],
    args: {},
    handler: function(actor, resolved) {
        if (!actor.hasTag('admin')) {
            actor.send('Huh?\r\n');
            return;
        }

        actor.send("Starting link wizard. Type 'cancel' or 'quit' to exit at any time.\r\n");
        tapestry.flows.trigger(actor.entityId, "admin_link");
    }
});
