tapestry.commands.register({
    name: 'put',
    description: 'Put an item into a container.',
    priority: 0,
    handler: function(player, args) {
        if (args.length < 2) {
            player.send('Put what where?\r\n');
            return;
        }

        var keyword = args[0];

        if (keyword === 'all' || keyword.indexOf('all.') === 0) {
            var containerKeyword = args.slice(1).join(' ');
            var result = tapestry.inventory.putAllInContainer(player.entityId, containerKeyword);
            if (!result) {
                player.send("You don't see that container here.\r\n");
                return;
            }
            if (result.denied) {
                player.send("You can't put items in that.\r\n");
                return;
            }
            if (!result.items || result.items.length === 0) {
                if (result.stopReason === 'full' || result.stopReason === 'too_heavy') {
                    player.send((result.containerName || containerKeyword) + ' is full.\r\n');
                } else {
                    player.send("You have nothing to put in there.\r\n");
                }
                return;
            }
            var cName = result.containerName || containerKeyword;
            result.items.forEach(function(r) {
                player.send('You put ' + r.name + ' in ' + cName + '.\r\n');
            });
            player.sendToRoom(player.name + ' puts some items away.\r\n');
            return;
        }

        var cleanArgs = args.filter(function(a) { return a.toLowerCase() !== 'in'; });
        if (cleanArgs.length < 2) {
            player.send('Put what where?\r\n');
            return;
        }
        var itemKeyword = cleanArgs[0];
        var containerKeyword = cleanArgs.slice(1).join(' ');

        var itemRef = tapestry.inventory.findByKeyword(player.entityId, itemKeyword);
        var containerRef = tapestry.inventory.findByKeyword(player.entityId, containerKeyword)
            || tapestry.inventory.findInRoom(player.entityId, containerKeyword);
        var itemName = itemRef ? itemRef.name : itemKeyword;
        var containerName = containerRef ? containerRef.name : containerKeyword;

        var result = tapestry.inventory.putInContainer(player.entityId, itemKeyword, containerKeyword);
        if (!result) {
            player.send("You can't do that.\r\n");
            return;
        }
        if (result.success) {
            player.send('You put ' + itemName + ' in ' + containerName + '.\r\n');
            player.sendToRoom(player.name + ' puts something in ' + containerName + '.\r\n');
        } else if (result.reason === 'is_container') {
            player.send("You can't put containers in containers.\r\n");
        } else if (result.reason === 'item_not_found') {
            player.send("You aren't carrying that.\r\n");
        } else if (result.reason === 'container_not_found') {
            player.send("You don't see that container here.\r\n");
        } else if (result.reason === 'full') {
            player.send(containerName + ' is full.\r\n');
        } else if (result.reason === 'not_container') {
            player.send("You can't put things in that.\r\n");
        } else if (result.reason === 'too_heavy') {
            player.send("That would be too heavy for " + containerName + ".\r\n");
        } else {
            player.send("You can't do that.\r\n");
        }
    }
});
