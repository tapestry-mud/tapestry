tapestry.commands.register({
    name: 'wear',
    description: 'Wear an item from your inventory.',
    category: 'inventory',
    roles: ['player'],
    args: {
        item: { type: 'inventory', required: true, bulk: true }
    },
    handler: function(actor, resolved) {
        var item = resolved.item;

        if (Array.isArray(item)) {
            if (item.length === 0) {
                actor.send("You aren't carrying anything to wear.\r\n");
                return;
            }
            var wore = false;
            var slotsFull = {};
            var counts = {};
            var totals = {};
            var slots = tapestry.equipment.getSlots(actor.entityId);
            if (slots) {
                slots.forEach(function(s) {
                    var base = s.slot.indexOf(':') >= 0 ? s.slot.substring(0, s.slot.indexOf(':')) : s.slot;
                    if (!totals[base]) {
                        totals[base] = 0;
                        counts[base] = 0;
                    }
                    totals[base]++;
                    if (!s.empty) {
                        counts[base]++;
                    }
                });
                Object.keys(totals).forEach(function(base) {
                    if (counts[base] >= totals[base]) {
                        slotsFull[base] = true;
                    }
                });
            }
            item.forEach(function(i) {
                var details = tapestry.inventory.getItemDetails(actor.entityId, i.id);
                if (details && details.slot && !slotsFull[details.slot]) {
                    var result = tapestry.equipment.equip(actor.entityId, i.id, details.slot);
                    if (result) {
                        actor.send('You wear ' + i.name + '.\r\n');
                        wore = true;
                        if (!counts[details.slot]) {
                            counts[details.slot] = 0;
                        }
                        counts[details.slot]++;
                        if (counts[details.slot] >= (totals[details.slot] || 1)) {
                            slotsFull[details.slot] = true;
                        }
                    }
                }
            });
            if (!wore) {
                actor.send("Nothing you're carrying can be worn.\r\n");
            }
            return;
        }

        var details = tapestry.inventory.getItemDetails(actor.entityId, item.id);
        if (!details || !details.slot) {
            actor.send("You can't wear that.\r\n");
            return;
        }
        var result = tapestry.equipment.equip(actor.entityId, item.id, details.slot);
        if (result) {
            if (result.displaced) {
                actor.send('You remove ' + result.displaced.name + '.\r\n');
            }
            actor.send('You wear ' + item.name + '.\r\n');
            actor.sendToRoom(actor.name + ' wears ' + item.name + '.\r\n');
        } else {
            actor.send("You can't wear that there.\r\n");
        }
    }
});
