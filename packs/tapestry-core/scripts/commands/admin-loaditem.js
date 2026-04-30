tapestry.commands.register({
    name: 'loaditem',
    admin: true,
    description: 'Add an item from a template ID to your inventory.',
    priority: 0,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (args.length === 0) {
            player.send('Usage: loaditem [template-id]\r\n');
            return;
        }
        var templateId = args.join(' ');
        var result = tapestry.items.spawnToInventory(templateId, player.entityId);
        if (result) {
            player.send('Loaded ' + result.name + ' into your inventory.\r\n');
        } else {
            player.send('Unknown item template: ' + templateId + '\r\n');
        }
    }
});
