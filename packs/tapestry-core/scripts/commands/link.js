tapestry.commands.register({
    name: 'link',
    aliases: [],
    description: 'Link rooms across packs via guided flow.',
    category: 'admin',
    admin: true,
    args: {},
    handler: function(actor, resolved) {
        actor.send("Starting link wizard. Type 'cancel' or 'quit' to exit at any time.\r\n");
        tapestry.flows.trigger(actor.entityId, "admin_link");
    }
});
