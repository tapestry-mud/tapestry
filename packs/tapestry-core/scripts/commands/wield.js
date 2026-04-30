tapestry.commands.register({
    name: 'wield',
    description: 'Wield a weapon from your inventory.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Wield what?\r\n');
            return;
        }
        var keyword = args[0];
        var found = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!found) {
            player.send("You aren't carrying that.\r\n");
            return;
        }
        var result = tapestry.equipment.equip(player.entityId, keyword, 'wield');
        if (result) {
            if (result.displaced) {
                player.send('You remove ' + result.displaced.name + '.\r\n');
            }
            player.send('You wield ' + found.name + '.\r\n');
            player.sendToRoom(player.name + ' wields ' + found.name + '.\r\n');
        } else {
            player.send("You can't wield that.\r\n");
        }
    }
});
