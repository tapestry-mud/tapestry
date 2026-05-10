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

function lookAtTarget(actor, resolved) {
    if (!resolved) { return false; }

    if (resolved.source === 'inventory') {
        var item = tapestry.inventory.examineItem(actor.entityId, resolved.name);
        if (item) {
            actor.send('\r\n<highlight>--- ' + item.name + ' ---</highlight>\r\n');
            if (item.slotDisplay) { actor.send('  Slot: ' + item.slotDisplay + '\r\n'); }
            if (item.weight > 0) { actor.send('  Weight: ' + item.weight + '\r\n'); }
            if (item.rarity) {
                actor.send('  Rarity: <item.' + item.rarity + '>' + item.rarity + '</item.' + item.rarity + '>\r\n');
            }
            if (item.modifiers && item.modifiers.length > 0) {
                actor.send('  Modifiers:\r\n');
                item.modifiers.forEach(function(m) {
                    var sign = m.value >= 0 ? '+' : '';
                    actor.send('    ' + sign + m.value + ' ' + m.stat + '\r\n');
                });
            }
            actor.send('<highlight>---' + Array(item.name.length + 3).join('-') + '---</highlight>\r\n');
            if (item.isContainer) {
                if (item.contents && item.contents.length > 0) {
                    actor.send(item.name + ' contains:\r\n');
                    item.contents.forEach(function(c) { actor.send('  ' + c.name + '\r\n'); });
                } else {
                    actor.send(item.name + ' is empty.\r\n');
                }
            }
            return true;
        }
    }

    var details = tapestry.world.getEntity(resolved.id);
    if (!details) { return false; }

    if (resolved.type === 'npc') {
        actor.send('\r\n<npc>--- ' + details.name + ' ---</npc>\r\n');
        if (details.properties && details.properties.description) {
            actor.send('  ' + details.properties.description + '\r\n');
        }
        actor.send('<npc>---' + Array(details.name.length + 3).join('-') + '---</npc>\r\n');
        var healthText = getHealthTierText(resolved.id);
        if (healthText) { actor.send('  ' + details.name + ' ' + healthText + '.\r\n'); }
        return true;
    }

    if (resolved.type === 'player') {
        var playerHealth = getHealthTierText(resolved.id);
        actor.send('\r\n<player>' + details.name + ' is here.</player>\r\n');
        if (playerHealth) { actor.send('  ' + details.name + ' ' + playerHealth + '.\r\n'); }
        return true;
    }

    // item or container in room
    actor.send('\r\n<highlight>--- ' + details.name + ' ---</highlight>\r\n');
    if (details.properties && details.properties.description) {
        actor.send('  ' + details.properties.description + '\r\n');
    }
    actor.send('<highlight>---' + Array(details.name.length + 3).join('-') + '---</highlight>\r\n');
    if (details.type === 'container') {
        var contents = details.inventory || [];
        if (contents.length > 0) {
            actor.send(details.name + ' contains:\r\n');
            contents.forEach(function(c) { actor.send('  ' + c.name + '\r\n'); });
        } else {
            actor.send(details.name + ' is empty.\r\n');
        }
    }
    return true;
}

function showCombatStatusInRoom(actor) {
    var npcs = tapestry.world.getEntitiesInRoom(actor.roomId, "npc");
    if (!npcs || npcs.length === 0) { return; }

    var shown = {};
    for (var i = 0; i < npcs.length; i++) {
        var entity = npcs[i];
        if (tapestry.combat.isInCombat(entity.id) && !shown[entity.id]) {
            shown[entity.id] = true;
            var healthText = getHealthTierText(entity.id);
            var suffix = healthText ? ' (' + healthText + ')' : '';
            actor.send('<combat_status>' + entity.name + ' is here, fighting!' + suffix + '</combat_status>\r\n');
        }
    }
}

function buildRoomLookPayload(actor) {
    var roomId = tapestry.world.getEntityRoomId(actor.entityId);
    if (!roomId) { return null; }

    var roomName = tapestry.world.getRoomName(roomId) || '';
    var roomDesc = tapestry.world.getRoomDescription(roomId) || '';
    var exits = tapestry.world.getRoomExits(actor.entityId);

    var all = tapestry.world.getVisibleEntities(roomId, actor.entityId);

    var entities = [];
    var items = [];

    for (var i = 0; i < all.length; i++) {
        var e = all[i];
        if (e.type === 'npc') {
            entities.push({ name: e.name, type: 'npc', tags: e.tags || [] });
        } else if (e.type === 'player' && e.id !== actor.entityId) {
            entities.push({ name: e.name, type: 'player', tags: e.tags || [] });
        } else if (e.type === 'item' || e.type.startsWith('item:') || e.type === 'container') {
            items.push({ name: e.name, quantity: 1 });
        }
    }

    return {
        status: 'ok',
        type: 'room',
        name: roomName,
        description: roomDesc,
        exits: exits,
        entities: entities,
        items: items
    };
}

tapestry.commands.register({
    name: 'look',
    aliases: ['l'],
    description: 'Look at the room, an entity, or an item.',
    category: 'info',
    roles: ['player'],
    args: {
        target: { type: 'visible', required: false }
    },
    handler: function(actor, resolved) {
        var restState = tapestry.rest.getRestState(actor.entityId);
        if (restState === 'sleeping') {
            actor.send("You can't see anything, you're asleep.\r\n");
            return;
        }

        var target = resolved.target;

        if (!target) {
            var lookPayload = buildRoomLookPayload(actor);
            if (lookPayload) {
                tapestry.gmcp.send(actor.entityId, 'Response.Look', lookPayload);
                tapestry.respond.suppress(actor.entityId);
            }

            tapestry.world.sendRoomDescription(actor.entityId);

            var lookRoomId = tapestry.world.getEntityRoomId(actor.entityId);
            if (lookRoomId) {
                var roomExits = tapestry.world.getRoomExits(actor.entityId);
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
                    actor.send('<exits>Doors: ' + doorParts.join(', ') + '</exits>\r\n');
                }

                var kwExits = tapestry.portals.getKeywordExits(lookRoomId);
                if (kwExits.length > 0) {
                    var seeNames = [];
                    for (var ki = 0; ki < kwExits.length; ki++) {
                        seeNames.push(kwExits[ki].name || kwExits[ki].keyword);
                    }
                    actor.send('<exits>You see: ' + seeNames.join(', ') + '</exits>\r\n');
                }
            }

            showCombatStatusInRoom(actor);
            return;
        }

        if (!lookAtTarget(actor, target)) {
            actor.send("You don't see that here.\r\n");
        }
    }
});

tapestry.commands.register({
    name: 'examine',
    aliases: ['ex', 'exa'],
    description: 'Examine an item or entity in detail.',
    category: 'info',
    roles: ['player'],
    args: {
        target: { type: 'visible', required: true }
    },
    handler: function(actor, resolved) {
        if (!lookAtTarget(actor, resolved.target)) {
            actor.send("You don't see that here.\r\n");
        }
    }
});
