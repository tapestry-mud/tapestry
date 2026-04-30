tapestry.commands.register({
    name: 'score',
    description: 'Display your character stats and status.',
    priority: 0,
    handler: function(player, args) {
        var s = player.stats;
        var hpName = tapestry.stats.getDisplayName('hp');
        var resName = tapestry.stats.getDisplayName('resource');
        var movName = tapestry.stats.getDisplayName('movement');

        var identitySection = {
            rows: [{ type: 'title', left: player.name }]
        };

        var tracks = tapestry.progression.getTracks();
        var proficiencySection = null;
        if (tracks && tracks.length > 0) {
            var profRows = [];
            for (var t = 0; t < tracks.length; t++) {
                var info = tapestry.progression.getInfo(player.entityId, tracks[t].name);
                if (!info) { continue; }
                var pct = 0;
                if (info.xpToNext > 0) {
                    var progressInLevel = info.xp - info.currentLevelThreshold;
                    var levelRange = info.xpToNext + progressInLevel;
                    pct = Math.floor((progressInLevel / levelRange) * 100);
                } else if (info.level >= info.maxLevel) {
                    pct = 100;
                }
                var tName = tracks[t].name.charAt(0).toUpperCase() + tracks[t].name.slice(1);
                profRows.push({
                    type: 'text',
                    content: '  ' + tName + ': Level ' + info.level +
                             '  XP: ' + info.xp + ' / ' + (info.xp + info.xpToNext) +
                             ' (' + pct + '%)'
                });
            }
            if (profRows.length > 0) {
                proficiencySection = { separatorAbove: 'minor', rows: profRows };
            }
        }

        var hpPct  = s.maxHp       > 0 ? Math.floor(s.hp       / s.maxHp       * 100) : 0;
        var resPct = s.maxResource > 0 ? Math.floor(s.resource / s.maxResource * 100) : 0;
        var movPct = s.maxMovement > 0 ? Math.floor(s.movement / s.maxMovement * 100) : 0;

        var vitalsSection = {
            separatorAbove: 'minor',
            rows: [
                { type: 'cell', cells: [
                    { content: '  ' + hpName,  width: 16 },
                    { type: 'progress', value: s.hp,       max: s.maxHp,       width: 22 },
                    { content: s.hp       + ' / ' + s.maxHp,       width: 14, align: 'right' },
                    { content: '', width: 2 },
                    { content: '(' + hpPct  + '%)', width: 'fill', align: 'left' }
                ]},
                { type: 'cell', cells: [
                    { content: '  ' + resName, width: 16 },
                    { type: 'progress', value: s.resource,  max: s.maxResource, width: 22 },
                    { content: s.resource + ' / ' + s.maxResource, width: 14, align: 'right' },
                    { content: '', width: 2 },
                    { content: '(' + resPct + '%)', width: 'fill', align: 'left' }
                ]},
                { type: 'cell', cells: [
                    { content: '  ' + movName, width: 16 },
                    { type: 'progress', value: s.movement,  max: s.maxMovement, width: 22 },
                    { content: s.movement + ' / ' + s.maxMovement, width: 14, align: 'right' },
                    { content: '', width: 2 },
                    { content: '(' + movPct + '%)', width: 'fill', align: 'left' }
                ]}
            ]
        };

        var alignment = tapestry.alignment.get(player.entityId);
        var bucket = tapestry.alignment.bucket(player.entityId);
        var attribSection = {
            separatorAbove: 'minor',
            rows: [
                { type: 'cell', cells: [
                    { content: '  Str: ' + s.strength,      width: 26 },
                    { content: 'Int: ' + s.intelligence,     width: 26 },
                    { content: 'Wis: ' + s.wisdom,           width: 'fill' }
                ]},
                { type: 'cell', cells: [
                    { content: '  Dex: ' + s.dexterity,     width: 26 },
                    { content: 'Con: ' + s.constitution,     width: 26 },
                    { content: 'Luc: ' + s.luck,             width: 'fill' }
                ]},
                { type: 'text', content: '  Alignment: ' + alignment + ' [' + bucket + ']' }
            ]
        };

        var gold = tapestry.currency.getGold(player.entityId);
        var goldSection = {
            separatorAbove: 'minor',
            rows: [{ type: 'text', content: '  Gold: ' + gold }]
        };

        var susValue = tapestry.consumables.getSustenance(player.entityId);
        var susTier = tapestry.consumables.getSustenanceTier(player.entityId);
        var susPct = Math.floor(susValue);
        var susSection = {
            separatorAbove: 'minor',
            rows: [{ type: 'text', content: '  Hunger: ' + susTier + ' (' + susPct + '%)' }]
        };

        var sections = [identitySection];
        if (proficiencySection) { sections.push(proficiencySection); }
        sections.push(vitalsSection);
        sections.push(attribSection);
        sections.push(susSection);
        sections.push(goldSection);

        var output = tapestry.ui.panel({ sections: sections });
        player.send('\r\n' + output + '\r\n');
    }
});
