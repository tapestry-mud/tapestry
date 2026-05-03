tapestry.commands.register({
    name: 'link',
    aliases: [],
    description: 'Link rooms across packs via guided flow.',
    priority: 10,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        player.send("Starting link wizard. Type 'cancel' or 'quit' to exit at any time.\r\n");
        tapestry.flows.trigger(player.entityId, "admin_link");
    }
});
