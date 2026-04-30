tapestry.commands.register({
    name: 'recite',
    description: 'Recite a scroll from your inventory.',
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Recite what?\r\n');
            return;
        }
        var keyword = args.join(' ');
        var item = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!item) {
            player.send("You aren't carrying that.\r\n");
            return;
        }
        var itemType = tapestry.world.getProperty(item.id, 'item_type');
        if (itemType !== 'scroll') {
            player.send("You can't recite that.\r\n");
            return;
        }
        var result = tapestry.consumables.consume(player.entityId, item.id);
        if (result && result.success) {
            player.send('You recite ' + item.name + '.\r\n');
            player.sendToRoom(player.name + ' recites ' + item.name + '.\r\n');
        } else {
            player.send("You can't recite that.\r\n");
        }
    }
});
