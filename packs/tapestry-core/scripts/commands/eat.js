tapestry.commands.register({
    name: 'eat',
    description: 'Eat food from your inventory.',
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Eat what?\r\n');
            return;
        }
        var keyword = args.join(' ');
        var item = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!item) {
            player.send("You aren't carrying that.\r\n");
            return;
        }
        var itemType = tapestry.world.getProperty(item.id, 'item_type');
        if (itemType !== 'food') {
            player.send("You can't eat that.\r\n");
            return;
        }
        var result = tapestry.consumables.consume(player.entityId, item.id);
        if (result && result.success) {
            player.send('You eat ' + item.name + '.\r\n');
            player.sendToRoom(player.name + ' eats ' + item.name + '.\r\n');
        } else if (result && result.reason === 'nocharges') {
            player.send("It's empty.\r\n");
        } else {
            player.send("You can't eat that.\r\n");
        }
    }
});
