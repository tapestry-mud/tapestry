tapestry.commands.register({
    name: 'spawn',
    admin: true,
    description: 'Spawn a mob from a template ID into your room.',
    priority: 0,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (args.length === 0) {
            player.send('Usage: spawn [template-id]\r\n');
            return;
        }
        var templateId = args.join(' ');
        var result = tapestry.mobs.spawnMob(templateId, player.roomId);
        if (result) {
            player.send('Spawned: ' + result.name + '\r\n');
        } else {
            player.send('Unknown template: ' + templateId + '\r\n');
        }
    }
});
