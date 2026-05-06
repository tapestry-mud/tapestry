tapestry.commands.register({
    name: 'sac',
    aliases: ['sacrifice'],
    description: 'Sacrifice a corpse to remove it from the world.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Sacrifice what?\r\n');
            return;
        }

        var keyword = args.join(' ');
        var found = tapestry.inventory.findInRoom(player.entityId, keyword);
        if (!found) {
            player.send("You don't see that here.\r\n");
            return;
        }

        if (!tapestry.world.hasTag(found.id, 'corpse')) {
            player.send("You can only sacrifice corpses.\r\n");
            return;
        }

        if (tapestry.world.hasTag(found.id, 'player_corpse')) {
            var contents = tapestry.inventory.getContents(found.id);
            if (contents.length > 0) {
                player.send("You cannot sacrifice a player corpse that still has belongings in it.\r\n");
                return;
            }
        }

        var corpseName = found.name;
        var isPlayerCorpse = tapestry.world.hasTag(found.id, 'player_corpse');
        var corpseEntity = tapestry.world.getEntity(found.id);
        var level = corpseEntity && corpseEntity.properties ? (corpseEntity.properties.level || 0) : 0;

        destroyWithContents(found.id);

        player.send('You sacrifice ' + corpseName + ' to the heavens.\r\n');
        player.sendToRoom(player.name + ' sacrifices ' + corpseName + ' to the heavens.\r\n');

        if (!isPlayerCorpse && level > 0) {
            tapestry.currency.addGold(player.entityId, level, "sac");
            var coinWord = level === 1 ? 'coin' : 'coins';
            player.send('The heavens reward you with ' + level + ' gold ' + coinWord + '.\r\n');
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
