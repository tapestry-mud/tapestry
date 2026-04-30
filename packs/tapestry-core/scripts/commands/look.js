function getHealthTierText(entityId) {
    var stats = tapestry.stats.get(entityId);
    if (!stats) { return null; }
    var hp = stats.hp;
    var maxHp = stats.maxHp;
    if (maxHp <= 0) { return "is near death"; }
    var pct = Math.floor((hp / maxHp) * 100);
    if (pct >= 100) { return "is in perfect health"; }
    if (pct >= 75) { return "has a few scratches"; }
    if (pct >= 50) { return "has some small wounds"; }
    if (pct >= 35) { return "is wounded"; }
    if (pct >= 20) { return "is badly wounded"; }
    if (pct >= 10) { return "is bleeding profusely"; }
    return "is near death";
}

function lookAtTarget(player, keyword) {
    var item = tapestry.inventory.examineItem(player.entityId, keyword);
    if (item) {
        player.send('\r\n<highlight>--- ' + item.name + ' ---</highlight>\r\n');
        if (item.slotDisplay) {
            player.send('  Slot: ' + item.slotDisplay + '\r\n');
        }
        if (item.weight > 0) {
            player.send('  Weight: ' + item.weight + '\r\n');
        }
        if (item.rarity) {
            player.send('  Rarity: <item.' + item.rarity + '>' + item.rarity + '</item.' + item.rarity + '>\r\n');
        }
        if (item.modifiers && item.modifiers.length > 0) {
            player.send('  Modifiers:\r\n');
            item.modifiers.forEach(function(m) {
                var sign = m.value >= 0 ? '+' : '';
                player.send('    ' + sign + m.value + ' ' + m.stat + '\r\n');
            });
        }
        player.send('<highlight>---' + Array(item.name.length + 3).join('-') + '---</highlight>\r\n');
        if (item.isContainer) {
            if (item.contents && item.contents.length > 0) {
                player.send(item.name + ' contains:\r\n');
                item.contents.forEach(function(c) {
                    player.send('  ' + c.name + '\r\n');
                });
            } else {
                player.send(item.name + ' is empty.\r\n');
            }
        }
        return true;
    }

    var npcs = tapestry.world.getEntitiesInRoom(player.roomId, "npc");
    if (npcs && npcs.length > 0) {
        var lowerKeyword = keyword.toLowerCase();
        for (var i = 0; i < npcs.length; i++) {
            if (npcs[i].name.toLowerCase().indexOf(lowerKeyword) >= 0) {
                var details = tapestry.world.getEntity(npcs[i].id);
                player.send('\r\n<npc>--- ' + details.name + ' ---</npc>\r\n');
                if (details.properties && details.properties.description) {
                    player.send('  ' + details.properties.description + '\r\n');
                }
                player.send('<npc>---' + Array(details.name.length + 3).join('-') + '---</npc>\r\n');
                var healthText = getHealthTierText(npcs[i].id);
                if (healthText) {
                    player.send('  ' + details.name + ' ' + healthText + '.\r\n');
                }
                return true;
            }
        }
    }

    var players = tapestry.world.getEntitiesInRoom(player.roomId, "player");
    if (players && players.length > 0) {
        var lowerKeyword = keyword.toLowerCase();
        for (var i = 0; i < players.length; i++) {
            if (players[i].name.toLowerCase().indexOf(lowerKeyword) >= 0) {
                var healthText = getHealthTierText(players[i].id);
                player.send('\r\n<player>' + players[i].name + ' is here.</player>\r\n');
                if (healthText) {
                    player.send('  ' + players[i].name + ' ' + healthText + '.\r\n');
                }
                return true;
            }
        }
    }

    return false;
}

function showCombatStatusInRoom(player) {
    var npcs = tapestry.world.getEntitiesInRoom(player.roomId, "npc");
    if (!npcs || npcs.length === 0) { return; }

    var shown = {};
    for (var i = 0; i < npcs.length; i++) {
        var entity = npcs[i];
        if (tapestry.combat.isInCombat(entity.id) && !shown[entity.id]) {
            shown[entity.id] = true;
            var healthText = getHealthTierText(entity.id);
            var suffix = healthText ? ' (' + healthText + ')' : '';
            player.send('<combat_status>' + entity.name + ' is here, fighting!' + suffix + '</combat_status>\r\n');
        }
    }
}

tapestry.commands.register({
    name: 'look',
    aliases: ['l'],
    description: 'Look at the room, an entity, or an item.',
    priority: 0,
    handler: function(player, args) {
        var restState = tapestry.rest.getRestState(player.entityId);
        if (restState === 'sleeping') {
            player.send("You can't see anything, you're asleep.\r\n");
            return;
        }
        if (args.length === 0) {
            tapestry.world.sendRoomDescription(player.entityId);

            var lookRoomId = tapestry.world.getEntityRoomId(player.entityId);
            if (lookRoomId) {
                var roomExits = tapestry.world.getRoomExits(player.entityId);
                var doorParts = [];
                for (var di = 0; di < roomExits.length; di++) {
                    var doorInfo = tapestry.doors.getDoor(lookRoomId, roomExits[di]);
                    if (doorInfo) {
                        var stateStr = doorInfo.isClosed
                            ? (doorInfo.isLocked ? 'closed, locked' : 'closed')
                            : 'open';
                        doorParts.push(roomExits[di] + ' (' + doorInfo.name + ', ' + stateStr + ')');
                    }
                }
                if (doorParts.length > 0) {
                    player.send('<exits>Doors: ' + doorParts.join(', ') + '</exits>\r\n');
                }

                var kwExits = tapestry.portals.getKeywordExits(lookRoomId);
                if (kwExits.length > 0) {
                    var seeNames = [];
                    for (var ki = 0; ki < kwExits.length; ki++) {
                        seeNames.push(kwExits[ki].name || kwExits[ki].keyword);
                    }
                    player.send('<exits>You see: ' + seeNames.join(', ') + '</exits>\r\n');
                }
            }

            showCombatStatusInRoom(player);
            return;
        }

        if (args.length >= 2 && args[0].toLowerCase() === 'in') {
            var containerKeyword = args.slice(1).join(' ');
            if (!lookAtTarget(player, containerKeyword)) {
                player.send("You don't see that here.\r\n");
            }
            return;
        }

        var keyword = args.join(' ');
        if (!lookAtTarget(player, keyword)) {
            player.send("You don't see that here.\r\n");
        }
    }
});

tapestry.commands.register({
    name: 'examine',
    aliases: ['ex', 'exa'],
    description: 'Examine an item or entity in detail.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Examine what?\r\n');
            return;
        }
        var keyword = args.join(' ');
        if (!lookAtTarget(player, keyword)) {
            player.send("You don't see that here.\r\n");
        }
    }
});
