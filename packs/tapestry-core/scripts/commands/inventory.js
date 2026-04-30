tapestry.commands.register({
    name: 'inventory',
    aliases: ['i'],
    description: 'Show items you are carrying.',
    priority: 0,
    handler: function(player, args) {
        var stacks = tapestry.stacking.getStacks(player.entityId);
        var contentRows = [{ type: 'empty' }];

        if (!stacks || stacks.length === 0) {
            contentRows.push({ type: 'text', content: '  You are carrying nothing.' });
        } else {
            stacks.forEach(function(stack) {
                var rarityTag = tapestry.rarity.formatInline(stack.rarityKey);
                var essenceGlyph = tapestry.essence.format(stack.essenceKey);
                var displayName = stack.name;
                if (essenceGlyph) { displayName = displayName + ' ' + essenceGlyph; }
                if (stack.quantity > 1) { displayName = displayName + ' (x' + stack.quantity + ')'; }
                var line = rarityTag ? rarityTag + ' ' + displayName : displayName;
                contentRows.push({ type: 'text', content: '  ' + line });
            });
        }

        contentRows.push({ type: 'empty' });
        var gold = tapestry.currency.getGold(player.entityId);
        contentRows.push({ type: 'text', content: '  Gold: ' + gold });

        var totalCount = stacks
            ? stacks.reduce(function(sum, s) { return sum + s.quantity; }, 0)
            : 0;

        var output = tapestry.ui.panel({
            sections: [
                { rows: [{ type: 'title', left: 'Inventory', right: totalCount + ' item(s)' }] },
                { separatorAbove: 'minor', rows: contentRows }
            ]
        });
        player.send('\r\n' + output + '\r\n');
    }
});
