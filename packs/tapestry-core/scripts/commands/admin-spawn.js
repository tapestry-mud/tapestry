tapestry.commands.register({
    name: 'spawn',
    description: 'Spawn a mob from a template ID into your room.',
    category: 'admin',
    admin: true,
    args: {
        templateId: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var templateId = resolved.templateId;
        var result = tapestry.mobs.spawnMob(templateId, actor.roomId);

        if (result) {
            actor.send('Spawned: ' + result.name + '\r\n');
        } else {
            actor.send('Unknown template: ' + templateId + '\r\n');
        }
    }
});
