tapestry.commands.register({
    name: 'give',
    description: 'Give an item to another player.',
    priority: 0,
    handler: function(player, args) {
        if (args.length < 2) {
            player.send('Give what to whom?\r\n');
            return;
        }
        var keyword = args[0];
        var targetName = args.slice(1).join(' ');

        var target = tapestry.inventory.findPlayerInRoom(player.entityId, targetName);
        if (!target) {
            player.send("You don't see them here.\r\n");
            return;
        }
        var found = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!found) {
            player.send("You aren't carrying that.\r\n");
            return;
        }
        var success = tapestry.inventory.give(player.entityId, target.id, keyword);
        if (success) {
            player.send('You give ' + found.name + ' to ' + target.name + '.\r\n');
            tapestry.world.send(target.id, player.name + ' gives you ' + found.name + '.\r\n');
            player.sendToRoom(player.name + ' gives ' + found.name + ' to ' + target.name + '.\r\n');
        } else {
            player.send("You can't give that.\r\n");
        }
    }
});
