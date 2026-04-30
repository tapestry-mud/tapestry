// packs/tapestry-core/scripts/progression/progression.js
// ============================================================
// Progression system for tapestry-core pack
// ============================================================

// --- Track Registration ---

tapestry.progression.registerTrack({
    name: "combat",
    max_level: 50,
    xp_formula: function(level) {
        // Gentle curve: 100, 220, 364, 536, 740...
        return Math.floor(100 * Math.pow(level, 1.3));
    },
    death_penalty: 0.1,
    on_level_up: function(entityId, trackName, newLevel) {
        // Auto-apply stat gains: +1 to two random stats per level
        var stats = ["str", "int", "wis", "dex", "con", "luc"];
        var first = stats[Math.floor(Math.random() * stats.length)];
        var second = stats[Math.floor(Math.random() * stats.length)];
        tapestry.stats.addBaseAttribute(entityId, first, 1);
        tapestry.stats.addBaseAttribute(entityId, second, 1);

        // Boost vitals every level
        tapestry.stats.addBaseAttribute(entityId, "max_hp", 5);
        tapestry.stats.addBaseAttribute(entityId, "max_resource", 3);
        tapestry.stats.addBaseAttribute(entityId, "max_movement", 2);

        var classId = tapestry.world.getProperty(entityId, 'class') || '';
        var classDef = classId ? tapestry.classes.get(classId) : null;
        var flavor = (classDef && classDef.level_up_flavor) || "Your skills improve.";
        var gains = "+1 " + first + ", +1 " + second + ", +5 hp, +3 mana, +2 mv";
        tapestry.world.send(entityId,
            "\r\n<highlight>*** " + flavor + " You are now level " + newLevel + "! ***</highlight>\r\n  " + gains + "\r\n");
    }
});

tapestry.progression.registerTrack({
    name: "magic",
    max_level: 50,
    xp_formula: function(level) {
        return Math.floor(100 * Math.pow(level, 1.3));
    },
    death_penalty: 0.1,
    on_level_up: function(entityId, trackName, newLevel) {
        var stats = ["int", "int", "wis", "wis", "dex", "luc"];
        var first = stats[Math.floor(Math.random() * stats.length)];
        var second = stats[Math.floor(Math.random() * stats.length)];
        tapestry.stats.addBaseAttribute(entityId, first, 1);
        tapestry.stats.addBaseAttribute(entityId, second, 1);
        tapestry.stats.addBaseAttribute(entityId, "max_resource", 5);
        tapestry.stats.addBaseAttribute(entityId, "max_hp", 2);
        tapestry.stats.addBaseAttribute(entityId, "max_movement", 1);

        var classId = tapestry.world.getProperty(entityId, 'class') || '';
        var classDef = classId ? tapestry.classes.get(classId) : null;
        var flavor = (classDef && classDef.level_up_flavor) || "Your magical power grows.";
        var gains = "+1 " + first + ", +1 " + second + ", +5 mana, +2 hp, +1 mv";
        tapestry.world.send(entityId,
            "\r\n<highlight>*** " + flavor + " You are now level " + newLevel + "! ***</highlight>\r\n  " + gains + "\r\n");
    }
});

// --- Default base XP by mob level ---

function baseXpForMobLevel(mobLevel) {
    // Simple curve: low-level mobs give modest XP, scales up
    return Math.floor(30 + (mobLevel * mobLevel * 2));
}

// --- Kill XP Handler ---

tapestry.events.on("combat.kill", function(event) {
    var killerId = event.sourceEntityId;
    var victimId = event.targetEntityId;

    if (!killerId || !victimId) {
        return;
    }

    var victim = tapestry.world.getEntity(victimId);
    if (!victim || victim.type === "player") {
        return; // No XP for killing players
    }

    var mobLevel = tapestry.world.getProperty(victimId, "level") || 1;
    var baseXp = tapestry.world.getProperty(victimId, "xp_value") || baseXpForMobLevel(mobLevel);

    // Get all combatants who were fighting this mob
    var combatants = tapestry.combat.getCombatants(victimId);
    if (!combatants || combatants.length === 0) {
        // Fallback: just the killer
        combatants = [killerId];
    }

    // Filter to players only
    var playerCombatants = [];
    for (var i = 0; i < combatants.length; i++) {
        var entity = tapestry.world.getEntity(combatants[i]);
        if (entity && entity.type === "player") {
            playerCombatants.push(combatants[i]);
        }
    }

    if (playerCombatants.length === 0) {
        return;
    }

    var victimRoom = tapestry.world.getEntityRoomId(victimId);
    var seen = {};
    for (var a = 0; a < playerCombatants.length; a++) {
        seen[playerCombatants[a]] = true;
    }
    for (var b = 0; b < playerCombatants.length; b++) {
        var groupMembers = getSameRoomGroupMembers(playerCombatants[b]);
        for (var c = 0; c < groupMembers.length; c++) {
            var memberId = groupMembers[c];
            if (!seen[memberId] && tapestry.world.getEntityRoomId(memberId) === victimRoom) {
                seen[memberId] = true;
                playerCombatants.push(memberId);
            }
        }
    }

    var share = tapestry.progression.groupShare(playerCombatants.length);

    for (var j = 0; j < playerCombatants.length; j++) {
        var playerId = playerCombatants[j];
        var playerLevel = tapestry.progression.getLevel(playerId, "combat");
        var scaledXp = tapestry.progression.calculateMobXp(playerLevel, mobLevel, baseXp);
        var finalXp = Math.floor(scaledXp * share);

        if (finalXp > 0) {
            tapestry.progression.grant(playerId, finalXp, "combat", "kill");
        }
    }
});

// --- XP Gain Message ---

tapestry.events.on("progression.xp.gained", function(event) {
    var data = event.data || {};
    var entityId = event.sourceEntityId;
    var amount = data.amount || 0;
    var source = data.source || "";

    if (entityId && source === "kill") {
        tapestry.world.send(entityId,
            "<experience>You gain " + amount + " experience.</experience>\r\n");
    }
});

// --- Death Penalty Handler ---

tapestry.events.on("player.death", function(event) {
    var data = event.data || {};
    var entityId = data.entityId;

    if (!entityId) {
        return;
    }

    var tracks = tapestry.progression.getTracks();
    for (var i = 0; i < tracks.length; i++) {
        var track = tracks[i];
        if (track.death_penalty > 0) {
            var info = tapestry.progression.getInfo(entityId, track.name);
            if (info) {
                var progressInLevel = info.xp - info.currentLevelThreshold;
                var loss = Math.floor(progressInLevel * track.death_penalty);
                if (loss > 0) {
                    tapestry.progression.deduct(entityId, loss, track.name);
                    tapestry.world.send(entityId,
                        "<death>You lose " + loss + " experience.</death>\r\n");
                }
            }
        }
    }
});
