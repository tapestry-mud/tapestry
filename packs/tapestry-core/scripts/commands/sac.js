tapestry.commands.register({
    name: 'sac',
    aliases: ['sacrifice'],
    description: 'Sacrifice a corpse to remove it from the world.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'room_item', required: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        if (!tapestry.world.hasTag(item.id, 'corpse')) {
            actor.send("You can only sacrifice corpses.\r\n");
            return;
        }

        if (tapestry.world.hasTag(item.id, 'player_corpse')) {
            var contents = tapestry.inventory.getContents(item.id);
            if (contents.length > 0) {
                actor.send("You cannot sacrifice a player corpse that still has belongings in it.\r\n");
                return;
            }
        }

        var isPlayerCorpse = tapestry.world.hasTag(item.id, 'player_corpse');
        var corpseEntity = tapestry.world.getEntity(item.id);
        var level = 0;
        if (corpseEntity && corpseEntity.properties) {
            level = corpseEntity.properties.level || 0;
        }

        destroyWithContents(item.id);

        actor.send('You sacrifice ' + item.name + ' to the heavens.\r\n');
        actor.sendToRoom(actor.name + ' sacrifices ' + item.name + ' to the heavens.\r\n');

        if (!isPlayerCorpse && level > 0) {
            tapestry.currency.addGold(actor.entityId, level, "sac");
            var coinWord = level === 1 ? 'coin' : 'coins';
            actor.send('The heavens reward you with ' + level + ' gold ' + coinWord + '.\r\n');
        }
    }
});

function destroyWithContents(entityId) {
    var contents = tapestry.inventory.getContents(entityId);
    for (var i = 0; i < contents.length; i++) {
        destroyWithContents(contents[i].id);
    }
    tapestry.world.removeEntity(entityId);
}
