tapestry.commands.register({
    name: 'inspect',
    admin: true,
    description: 'Show detailed stats, equipment, and properties for a target.',
    priority: 0,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (!args || args.length === 0) {
            player.send('Usage: inspect [target]\r\n');
            return;
        }

        var keyword = args.join(' ').toLowerCase();

        var ordinal = 1;
        var baseKeyword = keyword;
        var ordinalMatch = keyword.match(/^(\d+)\.(.+)$/);
        if (ordinalMatch) {
            ordinal = parseInt(ordinalMatch[1], 10);
            baseKeyword = ordinalMatch[2];
        }

        var target = null;
        if (keyword === 'self' || keyword === 'me') {
            target = { id: player.entityId, name: player.name };
        } else {
            var players = tapestry.world.getOnlinePlayers();
            var playerCount = 0;
            for (var i = 0; i < players.length; i++) {
                if (players[i].name.toLowerCase().indexOf(baseKeyword) !== -1 &&
                    tapestry.world.getEntityRoomId(players[i].id) === player.roomId) {
                    playerCount++;
                    if (playerCount === ordinal) {
                        target = { id: players[i].id, name: players[i].name };
                        break;
                    }
                }
            }
            if (!target) {
                var npcs = tapestry.world.getEntitiesInRoom(player.roomId, 'npc');
                var npcCount = 0;
                for (var n = 0; n < npcs.length; n++) {
                    if (npcs[n].name.toLowerCase().indexOf(baseKeyword) !== -1) {
                        npcCount++;
                        if (npcCount === ordinal) {
                            target = { id: npcs[n].id, name: npcs[n].name };
                            break;
                        }
                    }
                }
            }
        }

        if (!target) {
            player.send("Nothing named '" + keyword + "' here.\r\n");
            return;
        }

        var e = tapestry.world.getEntity(target.id);
        if (!e) {
            player.send("Cannot resolve entity.\r\n");
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
        player.send('[' + e.name + ']\r\n');
        player.send('Class: ' + cls + ' | Race: ' + race + ' | Level: ' + level + '\r\n');
        player.send('Stats:   STR ' + (s.strength||0) +
                    '  INT ' + (s.intelligence||0) +
                    '  WIS ' + (s.wisdom||0) +
                    '  DEX ' + (s.dexterity||0) +
                    '  CON ' + (s.constitution||0) +
                    '  LUC ' + (s.luck||0) + '\r\n');
        player.send('Vitals:  HP ' + (s.hp||0) + '/' + (s.max_hp||0) +
                    '  Resource ' + (s.resource||0) + '/' + (s.max_resource||0) +
                    '  Move ' + (s.movement||0) + '/' + (s.max_movement||0) + '\r\n');
        var gold = tapestry.currency.getGold(target.id);
        player.send('Gold:    ' + gold + '\r\n');
        var hunger = tapestry.consumables.getSustenance(target.id);
        var hungerTier = tapestry.consumables.getSustenanceTier(target.id);
        player.send('Hunger:  ' + hungerTier + ' (' + hunger + '%)\r\n');

        var profLines = [];
        for (var pk in allProps) {
            if (Object.prototype.hasOwnProperty.call(allProps, pk) && pk.indexOf('level:') === 0) {
                var tName = pk.slice(6);
                tName = tName.charAt(0).toUpperCase() + tName.slice(1);
                profLines.push('  ' + tName + ': Level ' + allProps[pk]);
            }
        }
        if (profLines.length) {
            player.send('Proficiency:\r\n' + profLines.join('\r\n') + '\r\n');
        }

        var tags = tapestry.world.getEntityTags ? tapestry.world.getEntityTags(target.id) : [];
        player.send('Flags:   ' + (tags && tags.length ? tags.join(', ') : '(none)') + '\r\n');

        var eq = e.equipment || {};
        var eqLines = [];
        for (var slot in eq) {
            if (Object.prototype.hasOwnProperty.call(eq, slot)) {
                eqLines.push(slot + ': ' + (eq[slot].name || eq[slot]));
            }
        }
        player.send('Equipment: ' + (eqLines.length ? eqLines.join(', ') : '(none)') + '\r\n');

        var inv = e.inventory || [];
        var invNames = [];
        for (var k = 0; k < inv.length; k++) {
            invNames.push(inv[k].name || String(inv[k]));
        }
        player.send('Inventory: ' + (invNames.length ? invNames.join(', ') : '(none)') + '\r\n');

        var props = e.properties || {};
        var propLines = [];
        for (var key in props) {
            if (Object.prototype.hasOwnProperty.call(props, key)) {
                propLines.push(key + ': ' + props[key]);
            }
        }
        player.send('Properties: ' + (propLines.length ? propLines.join(', ') : '(none)') + '\r\n');

        var alignment = tapestry.alignment.get(target.id);
        var bucket = tapestry.alignment.bucket(target.id);
        var history = tapestry.alignment.history(target.id);
        var recentHistory = history.slice(-5);
        var historyStr = recentHistory.length > 0
            ? recentHistory.map(function(h) {
                return (h.delta > 0 ? '+' : '') + h.delta + ' (' + h.reason + ')';
              }).join(', ')
            : 'none';
        player.send('Alignment: ' + alignment + ' [' + bucket + '] - last 5: ' + historyStr + '\r\n');
    }
});
