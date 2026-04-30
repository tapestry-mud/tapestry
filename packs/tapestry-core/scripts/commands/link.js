tapestry.commands.register({
    name: 'link',
    aliases: [],
    description: 'Link rooms across packs via guided flow.',
    priority: 10,
    handler: function(player, args) {
        tapestry.flows.trigger(player.entityId, "admin_link");
    }
});
