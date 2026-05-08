tapestry.commands.register({
    name: 'get',
    aliases: ['take'],
    description: 'Pick up an item from the room or a container.',
    category: 'inventory',
    roles: ['player', 'mob'],
    args: {
        item: { type: 'room_item', required: true, bulk: true },
        container: { type: 'container', required: false, prepositions: ['from', 'in'] }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;
        var container = resolved.container;

        if (container) {
            if (Array.isArray(item)) {
                var result = tapestry.inventory.getAllFromContainer(actor.entityId, container.keyword);
                if (!result) {
                    actor.send("You don't see that container here.\r\n");
                    return;
                }
                if (result.denied) {
                    actor.send("You can't take items from that.\r\n");
                    return;
                }
                if (!result.items || result.items.length === 0) {
                    actor.send("There's nothing in there.\r\n");
                    return;
                }
                result.items.forEach(function(r) {
                    actor.send('You get ' + r.name + '.\r\n');
                });
                actor.sendToRoom(actor.name + ' gets some items.\r\n');
                return;
            }
            var single = tapestry.inventory.getFromContainer(actor.entityId, item.keyword, container.keyword);
            if (!single) {
                actor.send("You don't see that there.\r\n");
                return;
            }
            if (single.denied) {
                actor.send("You can't take items from that.\r\n");
                return;
            }
            actor.send('You get ' + single.name + '.\r\n');
            actor.sendToRoom(actor.name + ' gets something.\r\n');
            return;
        }

        if (Array.isArray(item)) {
            if (item.length === 0) {
                actor.send("You don't see anything to pick up.\r\n");
                return;
            }
            item.forEach(function(i) {
                tapestry.inventory.pickUp(actor.entityId, i.keyword);
                actor.send('You pick up ' + i.name + '.\r\n');
            });
            actor.sendToRoom(actor.name + ' picks up some items.\r\n');
            return;
        }

        var tags = tapestry.world.getEntityTags(item.id);
        if (tags && tags.indexOf('no_get') !== -1) {
            actor.send("You can't pick that up.\r\n");
            return;
        }
        var success = tapestry.inventory.pickUp(actor.entityId, item.keyword);
        if (success) {
            actor.send('You pick up ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' picks up ' + item.name + '.\r\n');
        } else {
            actor.send("You can't carry that.\r\n");
        }
    }
});
