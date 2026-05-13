tapestry.commands.register({
    name: 'inspect',
    description: 'Show detailed stats, equipment, and properties for a target.',
    category: 'admin',
    admin: true,
    args: {
        entity: { type: 'visible', required: true, bypass_visibility: true }
    },
    handler: function(actor, resolved) {
        var target = resolved.entity;

        var e = tapestry.world.getEntity(target.id);
        if (!e) {
            actor.send("Cannot resolve entity.\r\n");
            return;
        }

        var cls = tapestry.world.getProperty(target.id, 'class') || '-';
        var race = tapestry.world.getProperty(target.id, 'race') || '-';
        var allProps = e.properties || {};
        var level = '-';
        for (var lk in allProps) {
            if (Object.prototype.hasOwnProperty.call(allProps, lk) && lk.indexOf('level:') === 0) {
                var lv = allProps[lk];
                if (level === '-' || lv > level) { level = lv; }
            }
        }
        if (level === '-' && allProps['level'] !== undefined) { level = allProps['level']; }

        var s = e.stats || {};
        actor.send('[' + e.name + ']\r\n');
        actor.send('Class: ' + cls + ' | Race: ' + race + ' | Level: ' + level + '\r\n');
        actor.send('Stats:   STR ' + (s.strength||0) +
                    '  INT ' + (s.intelligence||0) +
                    '  WIS ' + (s.wisdom||0) +
                    '  DEX ' + (s.dexterity||0) +
                    '  CON ' + (s.constitution||0) +
                    '  LUC ' + (s.luck||0) + '\r\n');
        actor.send('Vitals:  HP ' + (s.hp||0) + '/' + (s.max_hp||0) +
                    '  Resource ' + (s.resource||0) + '/' + (s.max_resource||0) +
                    '  Move ' + (s.movement||0) + '/' + (s.max_movement||0) + '\r\n');

        var gold = tapestry.currency.getGold(target.id);
        actor.send('Gold:    ' + gold + '\r\n');

        var hunger = tapestry.consumables.getSustenance(target.id);
        var hungerTier = tapestry.consumables.getSustenanceTier(target.id);
        actor.send('Hunger:  ' + hungerTier + ' (' + hunger + '%)\r\n');

        var profLines = [];
        for (var pk in allProps) {
            if (Object.prototype.hasOwnProperty.call(allProps, pk) && pk.indexOf('level:') === 0) {
                var tName = pk.slice(6);
                tName = tName.charAt(0).toUpperCase() + tName.slice(1);
                profLines.push('  ' + tName + ': Level ' + allProps[pk]);
            }
        }
        if (profLines.length) {
            actor.send('Proficiency:\r\n' + profLines.join('\r\n') + '\r\n');
        }

        var tags = tapestry.world.getEntityTags ? tapestry.world.getEntityTags(target.id) : [];
        actor.send('Flags:   ' + (tags && tags.length ? tags.join(', ') : '(none)') + '\r\n');

        var eq = e.equipment || {};
        var eqLines = [];
        for (var slot in eq) {
            if (Object.prototype.hasOwnProperty.call(eq, slot)) {
                eqLines.push(slot + ': ' + (eq[slot].name || eq[slot]));
            }
        }
        actor.send('Equipment: ' + (eqLines.length ? eqLines.join(', ') : '(none)') + '\r\n');

        var inv = e.inventory || [];
        var invNames = [];
        for (var k = 0; k < inv.length; k++) {
            invNames.push(inv[k].name || String(inv[k]));
        }
        actor.send('Inventory: ' + (invNames.length ? invNames.join(', ') : '(none)') + '\r\n');

        var props = e.properties || {};
        var propLines = [];
        for (var key in props) {
            if (Object.prototype.hasOwnProperty.call(props, key)) {
                propLines.push(key + ': ' + props[key]);
            }
        }
        actor.send('Properties: ' + (propLines.length ? propLines.join(', ') : '(none)') + '\r\n');

        var alignment = tapestry.alignment.get(target.id);
        var bucket = tapestry.alignment.bucket(target.id);
        var history = tapestry.alignment.history(target.id);
        var recentHistory = history.slice(-5);
        var historyStr = recentHistory.length > 0
            ? recentHistory.map(function(h) {
                return (h.delta > 0 ? '+' : '') + h.delta + ' (' + h.reason + ')';
              }).join(', ')
            : 'none';
        actor.send('Alignment: ' + alignment + ' [' + bucket + '] - last 5: ' + historyStr + '\r\n');
    }
});
