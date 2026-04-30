tapestry.commands.register({
    name: 'get',
    aliases: ['take'],
    description: 'Pick up an item from the room or a container.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Get what?\r\n');
            return;
        }

        var keyword = args[0];

        if ((keyword === 'all' || keyword.indexOf('all.') === 0) && args.length >= 2) {
            var containerKeyword = args.slice(1).join(' ');
            var result = tapestry.inventory.getAllFromContainer(player.entityId, containerKeyword);
            if (!result) {
                player.send("You don't see that container here.\r\n");
                return;
            }
            if (result.denied) {
                player.send("You can't take items from that.\r\n");
                return;
            }
            if (!result.items || result.items.length === 0) {
                player.send("There's nothing in there.\r\n");
                return;
            }
            result.items.forEach(function(r) {
                player.send('You get ' + r.name + '.\r\n');
            });
            player.sendToRoom(player.name + ' gets some items.\r\n');
            return;
        }

        if (keyword === 'all' || keyword.indexOf('all.') === 0) {
            var results = tapestry.inventory.getAll(player.entityId, keyword);
            if (!results || results.length === 0) {
                player.send("You don't see anything to pick up.\r\n");
                return;
            }
            results.forEach(function(r) {
                player.send('You pick up ' + r.name + '.\r\n');
            });
            if (results.length > 0) {
                player.sendToRoom(player.name + ' picks up some items.\r\n');
            }
            return;
        }

        if (args.length >= 2) {
            var itemKeyword = args[0];
            var containerKeyword = args.slice(1).join(' ');
            var result = tapestry.inventory.getFromContainer(player.entityId, itemKeyword, containerKeyword);
            if (result) {
                if (result.denied) {
                    player.send("You can't take items from that.\r\n");
                    return;
                }
                player.send('You get ' + result.name + '.\r\n');
                player.sendToRoom(player.name + ' gets something.\r\n');
                return;
            }
        }

        var found = tapestry.inventory.findInRoom(player.entityId, keyword);
        if (!found) {
            player.send("You don't see that here.\r\n");
            return;
        }
        var tags = tapestry.world.getEntityTags(found.id);
        if (tags && tags.indexOf('no_get') !== -1) {
            player.send("You can't pick that up.\r\n");
            return;
        }
        var success = tapestry.inventory.pickUp(player.entityId, keyword);
        if (success) {
            player.send('You pick up ' + found.name + '.\r\n');
            player.sendToRoom(player.name + ' picks up ' + found.name + '.\r\n');
        } else {
            player.send("You can't carry that.\r\n");
        }
    }
});
