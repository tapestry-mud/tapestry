tapestry.commands.register({
    name: 'give',
    description: 'Give an item to a player or NPC in the room.',
    category: 'inventory',
    roles: ['player', 'mob'],
    args: {
        item: { type: 'inventory', required: true },
        target: { type: 'entity', required: true, prepositions: ['to'] }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;
        var target = resolved.target;

        actor.send('You give ' + item.name + ' to ' + target.name + '.\r\n');
        tapestry.world.send(target.id, actor.name + ' gives you ' + item.name + '.\r\n');
        actor.sendToRoom(actor.name + ' gives ' + item.name + ' to ' + target.name + '.\r\n');
        tapestry.inventory.give(actor.entityId, target.id, item.keyword);

        // Receiver-perspective event
        tapestry.events.publish('entity.item.received', {
            itemId: item.id,
            itemName: item.name,
            templateId: item.templateId,
            giverId: actor.entityId,
            giverName: actor.name
        });

        // NPC onGive hook
        var templateId = tapestry.world.getProperty(target.id, 'template_id');
        if (templateId && target.type === 'npc') {
            tapestry.mobs.invokeHook(templateId, 'onGive',
                { entityId: target.id, name: target.name },
                { entityId: actor.entityId, name: actor.name, roomId: actor.roomId, stats: actor.stats },
                { entityId: item.id, name: item.name }
            );
        }
    }
});
