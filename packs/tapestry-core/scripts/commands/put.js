tapestry.commands.register({
    name: 'put',
    description: 'Put an item into a container.',
    category: 'inventory',
    roles: ['player', 'mob'],
    args: {
        item: { type: 'inventory', required: true, bulk: true },
        container: { type: 'container', required: true, prepositions: ['in'] }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;
        var container = resolved.container;

        // Bulk: put all [container] or put all.keyword [container]
        if (Array.isArray(item)) {
            var result = tapestry.inventory.putAllInContainer(actor.entityId, container.keyword);
            if (!result) {
                actor.send("You don't see that container here.\r\n");
                return;
            }
            if (result.denied) {
                actor.send("You can't put items in that.\r\n");
                return;
            }
            if (!result.items || result.items.length === 0) {
                if (result.stopReason === 'full' || result.stopReason === 'too_heavy') {
                    actor.send((result.containerName || container.name) + ' is full.\r\n');
                } else {
                    actor.send("You have nothing to put in there.\r\n");
                }
                return;
            }
            var cName = result.containerName || container.name;
            result.items.forEach(function(r) {
                actor.send('You put ' + r.name + ' in ' + cName + '.\r\n');
            });
            actor.sendToRoom(actor.name + ' puts some items away.\r\n');
            return;
        }

        // Single: put [item] in [container]
        var result = tapestry.inventory.putInContainer(actor.entityId, item.keyword, container.keyword);
        if (!result) {
            actor.send("You can't do that.\r\n");
            return;
        }
        if (result.success) {
            actor.send('You put ' + item.name + ' in ' + container.name + '.\r\n');
            actor.sendToRoom(actor.name + ' puts something in ' + container.name + '.\r\n');
        } else if (result.reason === 'is_container') {
            actor.send("You can't put containers in containers.\r\n");
        } else if (result.reason === 'item_not_found') {
            actor.send("You aren't carrying that.\r\n");
        } else if (result.reason === 'container_not_found') {
            actor.send("You don't see that container here.\r\n");
        } else if (result.reason === 'full') {
            actor.send(container.name + ' is full.\r\n');
        } else if (result.reason === 'not_container') {
            actor.send("You can't put things in that.\r\n");
        } else if (result.reason === 'too_heavy') {
            actor.send("That would be too heavy for " + container.name + ".\r\n");
        } else {
            actor.send("You can't do that.\r\n");
        }
    }
});
