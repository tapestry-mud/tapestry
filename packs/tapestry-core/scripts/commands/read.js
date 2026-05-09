tapestry.commands.register({
    name: 'read',
    description: 'Read a sign, letter, book, or other written item.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var keyword = resolved.item.toLowerCase();

        // Check room for readable items first (signs, plaques, etc.)
        var roomItem = tapestry.inventory.findInRoom(actor.entityId, keyword);
        if (roomItem) {
            var roomTags = tapestry.world.getEntityTags(roomItem.id);
            if (roomTags && roomTags.indexOf('readable') !== -1) {
                var roomText = tapestry.world.getProperty(roomItem.id, 'text');
                if (roomText) {
                    actor.send(roomText + '\r\n');
                } else {
                    actor.send('There is nothing written there.\r\n');
                }
                return;
            }
        }

        // Check player inventory for readable items (letters, books, scrolls)
        var carried = tapestry.inventory.findByKeyword(actor.entityId, keyword);
        if (carried) {
            var invTags = tapestry.world.getEntityTags(carried.id);
            if (invTags && invTags.indexOf('readable') !== -1) {
                var invText = tapestry.world.getProperty(carried.id, 'text');
                if (invText) {
                    actor.send(invText + '\r\n');
                } else {
                    actor.send('There is nothing written there.\r\n');
                }
                return;
            }
        }

        // Found something but it's not readable, or found nothing at all
        if (roomItem || carried) {
            actor.send("There's nothing written on that.\r\n");
        } else {
            actor.send("You don't see that here.\r\n");
        }
    }
});
