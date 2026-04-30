tapestry.commands.register({
    name: 'rest',
    description: 'Sit down and rest.',
    category: 'rest',
    handler: function(player, args) {
        var currentState = tapestry.rest.getRestState(player.entityId);
        if (currentState === 'resting' || currentState === 'sleeping') {
            player.send('You are already resting.\r\n');
            return;
        }
        if (tapestry.combat.isInCombat(player.entityId)) {
            player.send("You can't rest while fighting!\r\n");
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
                player.send("You can't rest on that.\r\n");
                return;
            }
        }

        var result = tapestry.rest.setRestState(player.entityId, 'resting', furnitureId);
        if (result && result.success) {
            if (furnitureName) {
                player.send('You rest on ' + furnitureName + '.\r\n');
                player.sendToRoom(player.name + ' sits down and rests on ' + furnitureName + '.\r\n');
            } else {
                player.send('You sit down and rest.\r\n');
                player.sendToRoom(player.name + ' sits down and rests.\r\n');
            }
        } else {
            player.send("You can't rest right now.\r\n");
        }
    }
});
