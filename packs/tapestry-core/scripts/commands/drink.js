tapestry.commands.register({
    name: 'drink',
    description: 'Drink from an item in your inventory.',
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Drink what?\r\n');
            return;
        }
        var keyword = args.join(' ');
        var item = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!item) {
            player.send("You aren't carrying that.\r\n");
            return;
        }
        var itemType = tapestry.world.getProperty(item.id, 'item_type');
        if (itemType !== 'drink') {
            player.send("You can't drink that.\r\n");
            return;
        }
        var charges = tapestry.world.getProperty(item.id, 'charges');
        if (charges !== undefined && charges !== null && charges <= 0) {
            player.send("It's empty.\r\n");
            return;
        }
        var result = tapestry.consumables.consume(player.entityId, item.id);
        if (result && result.success) {
            player.send('You drink from ' + item.name + '.\r\n');
            player.sendToRoom(player.name + ' drinks from ' + item.name + '.\r\n');
        } else if (result && result.reason === 'nocharges') {
            player.send("It's empty.\r\n");
        } else {
            player.send("You can't drink that.\r\n");
        }
    }
});
