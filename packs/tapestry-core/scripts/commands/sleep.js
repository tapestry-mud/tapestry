tapestry.commands.register({
    name: 'sleep',
    description: 'Lie down and sleep.',
    category: 'rest',
    handler: function(player, args) {
        var currentState = tapestry.rest.getRestState(player.entityId);
        if (currentState === 'sleeping') {
            player.send('You are already sleeping.\r\n');
            return;
        }
        if (tapestry.combat.isInCombat(player.entityId)) {
            player.send("You can't sleep while fighting!\r\n");
            return;
        }

        var furnitureId = null;
        var furnitureName = null;
        if (args.length > 0) {
            var keyword = args.join(' ');
            var furniture = tapestry.inventory.findInRoom(player.entityId, keyword);
            if (!furniture) { furniture = tapestry.inventory.findByKeyword(player.entityId, keyword); }
            if (furniture && furniture.properties && furniture.properties.rest_bonus !== undefined) {
                furnitureId = furniture.id;
                furnitureName = furniture.name;
            } else if (furniture) {
                player.send("You can't sleep on that.\r\n");
                return;
            }
        }

        var result = tapestry.rest.setRestState(player.entityId, 'sleeping', furnitureId);
        if (result && result.success) {
            if (furnitureName) {
                player.send('You lie down and sleep on ' + furnitureName + '.\r\n');
                player.sendToRoom(player.name + ' lies down and sleeps on ' + furnitureName + '.\r\n');
            } else {
                player.send('You lie down and sleep.\r\n');
                player.sendToRoom(player.name + ' lies down and sleeps.\r\n');
            }
        } else {
            player.send("You can't sleep right now.\r\n");
        }
    }
});
