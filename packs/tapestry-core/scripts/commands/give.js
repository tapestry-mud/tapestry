tapestry.commands.register({
    name: 'give',
    description: 'Give an item to a player or NPC in the room.',
    priority: 0,
    handler: function(player, args) {
        if (!args || args.length < 2) {
            player.send('Give what to whom?\r\n');
            return;
        }

        var keyword = String(args[0]);

        // Strip optional "to" preposition: "give blade to lan" or "give blade lan"
        var rest = Array.prototype.slice.call(args, 1);
        if (rest.length > 0 && String(rest[0]).toLowerCase() === 'to') {
            rest = Array.prototype.slice.call(rest, 1);
        }
        if (rest.length === 0) {
            player.send('Give what to whom?\r\n');
            return;
        }
        var targetRaw = rest.join(' ').toLowerCase();

        // Parse ordinal prefix: "2.lan" -> ordinal=2, name="lan"
        var targetOrdinal = 1;
        var targetName = targetRaw;
        var dotPos = targetRaw.indexOf('.');
        if (dotPos > 0) {
            var maybeOrdinal = parseInt(targetRaw.substring(0, dotPos), 10);
            if (!isNaN(maybeOrdinal) && maybeOrdinal >= 1) {
                targetOrdinal = maybeOrdinal;
                targetName = targetRaw.substring(dotPos + 1);
            }
        }

        function findNth(entities, name, n) {
            var count = 0;
            for (var i = 0; i < entities.length; i++) {
                if (entities[i].name.toLowerCase().indexOf(name) !== -1) {
                    count++;
                    if (count === n) { return entities[i]; }
                }
            }
            return null;
        }

        var found = tapestry.inventory.findByKeyword(player.entityId, keyword);
        if (!found) {
            player.send("You aren't carrying that.\r\n");
            return;
        }

        // Players first
        var players = tapestry.world.getEntitiesInRoom(player.roomId, 'player');
        var targetPlayer = null;
        for (var i = 0; i < players.length; i++) {
            if (players[i].id !== player.entityId) {
                targetPlayer = findNth(
                    players.filter(function(p) { return p.id !== player.entityId; }),
                    targetName,
                    targetOrdinal
                );
                break;
            }
        }

        if (targetPlayer) {
            var success = tapestry.inventory.give(player.entityId, targetPlayer.id, keyword);
            if (success) {
                player.send('You give ' + found.name + ' to ' + targetPlayer.name + '.\r\n');
                tapestry.world.send(targetPlayer.id, player.name + ' gives you ' + found.name + '.\r\n');
                player.sendToRoom(player.name + ' gives ' + found.name + ' to ' + targetPlayer.name + '.\r\n');
            } else {
                player.send("You can't give that.\r\n");
            }
            return;
        }

        // Then NPCs
        var npcs = tapestry.world.getEntitiesInRoom(player.roomId, 'npc');
        var npc = findNth(npcs, targetName, targetOrdinal);

        if (npc) {
            var templateId = tapestry.world.getProperty(npc.id, 'template_id');
            if (!templateId) {
                player.send(npc.name + " doesn't seem interested in that.\r\n");
                return;
            }
            var success = tapestry.inventory.give(player.entityId, npc.id, keyword);
            if (success) {
                player.send('You give ' + found.name + ' to ' + npc.name + '.\r\n');
                player.sendToRoom(player.name + ' gives ' + found.name + ' to ' + npc.name + '.\r\n');
            } else {
                player.send("You can't give that.\r\n");
                return;
            }
            tapestry.mobs.invokeHook(templateId, 'onGive',
                { entityId: npc.id, name: npc.name },
                { entityId: player.entityId, name: player.name, roomId: player.roomId, stats: player.stats },
                { entityId: found.id, name: found.name }
            );
            return;
        }

        player.send("You don't see them here.\r\n");
    }
});
