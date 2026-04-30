tapestry.commands.register({
    name: 'equipment',
    aliases: ['eq'],
    description: 'Show your equipped items.',
    priority: 0,
    handler: function(player, args) {
        var slots = tapestry.equipment.getSlots(player.entityId);
        var emptyText = tapestry.equipment.getEmptyText();
        var contentRows = [{ type: 'empty' }];

        if (!slots || slots.length === 0) {
            contentRows.push({ type: 'text', content: '  You are wearing nothing.' });
        } else {
            var maxSlotNameLen = slots.reduce(function(max, s) {
                return Math.max(max, s.slotDisplay.length);
            }, 0);
            var slotCellWidth = maxSlotNameLen + 4;

            slots.forEach(function(s) {
                var itemContent;
                if (s.empty) {
                    itemContent = '<subtle>' + emptyText + '</subtle>';
                } else {
                    var rarityTag = tapestry.rarity.formatInline(s.rarityKey);
                    var essenceGlyph = tapestry.essence.format(s.essenceKey);
                    itemContent = s.itemName;
                    if (essenceGlyph) { itemContent = itemContent + ' ' + essenceGlyph; }
                    if (rarityTag) { itemContent = rarityTag + ' ' + itemContent; }
                }

                contentRows.push({
                    type: 'cell',
                    cells: [
                        { content: '[' + s.slotDisplay + ']', width: slotCellWidth, align: 'right' },
                        { content: ' ' + itemContent, width: 'fill' }
                    ]
                });
            });
        }

        contentRows.push({ type: 'empty' });
        var output = tapestry.ui.panel({
            sections: [
                { rows: [{ type: 'title', left: 'Equipment' }] },
                { separatorAbove: 'minor', rows: contentRows }
            ]
        });
        player.send('\r\n' + output + '\r\n');
    }
});
