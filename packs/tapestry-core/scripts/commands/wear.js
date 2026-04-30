tapestry.commands.register({
    name: 'wear',
    description: 'Wear an item from your inventory.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Wear what?\r\n');
            return;
        }
        var keyword = args[0];

        if (keyword === 'all') {
            var items = tapestry.inventory.getContents(player.entityId);
            if (!items || items.length === 0) {
                player.send("You aren't carrying anything to wear.\r\n");
                return;
            }
            var wore = false;
            var slotsFull = {};
            var counts = {};
            var totals = {};
            var slots = tapestry.equipment.getSlots(player.entityId);
            if (slots) {
                slots.forEach(function(s) {
                    var base = s.slot.indexOf(':') >= 0 ? s.slot.substring(0, s.slot.indexOf(':')) : s.slot;
                    if (!totals[base]) { totals[base] = 0; counts[base] = 0; }
                    totals[base]++;
                    if (!s.empty) { counts[base]++; }
                });
                Object.keys(totals).forEach(function(base) {
                    if (counts[base] >= totals[base]) { slotsFull[base] = true; }
                });
            }
            items.forEach(function(item) {
                var details = tapestry.inventory.getItemDetails(player.entityId, item.id);
                if (details && details.slot && !slotsFull[details.slot]) {
                    var result = tapestry.equipment.equip(player.entityId, item.id, details.slot);
                    if (result) {
                        player.send('You wear ' + item.name + '.\r\n');
                        wore = true;
                        if (!counts[details.slot]) { counts[details.slot] = 0; }
                        counts[details.slot]++;
                        if (counts[details.slot] >= (totals[details.slot] || 1)) {
                            slotsFull[details.slot] = true;
                        }
                    }
                }
            });
            if (!wore) {
                player.send("Nothing you're carrying can be worn.\r\n");
            }
            return;
        }

        var found = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!found) {
            player.send("You aren't carrying that.\r\n");
            return;
        }
        var item = tapestry.inventory.getItemDetails(player.entityId, keyword);
        if (!item || !item.slot) {
            player.send("You can't wear that.\r\n");
            return;
        }
        var result = tapestry.equipment.equip(player.entityId, keyword, item.slot);
        if (result) {
            if (result.displaced) {
                player.send('You remove ' + result.displaced.name + '.\r\n');
            }
            player.send('You wear ' + found.name + '.\r\n');
            player.sendToRoom(player.name + ' wears ' + found.name + '.\r\n');
        } else {
            player.send("You can't wear that there.\r\n");
        }
    }
});
