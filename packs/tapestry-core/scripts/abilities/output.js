// --- Ability miss output ---
tapestry.events.on("ability.missed", function(event) {
    var data = event.data || {};
    var abilityName = data.abilityName || "ability";
    var sourceId = event.sourceEntityId;

    if (sourceId) {
        tapestry.world.send(sourceId,
            "<combat_miss>Your " + abilityName + " fails to connect!</combat_miss>\r\n");
    }

    var targetId = event.targetEntityId;
    if (targetId && targetId !== sourceId) {
        var sourceName = tapestry.world.getEntity(sourceId) ? tapestry.world.getEntity(sourceId).name : "Someone";
        tapestry.world.send(targetId,
            "<combat_miss>" + sourceName + "'s " + abilityName + " fails to connect!</combat_miss>\r\n");
    }
});

// --- Ability fizzle output ---
tapestry.events.on("ability.fizzled", function(event) {
    var data = event.data || {};
    var abilityId = data.abilityId || "ability";
    var reason = data.reason || "unknown";
    var sourceId = event.sourceEntityId;

    if (sourceId) {
        if (reason === "insufficient_resources") {
            tapestry.world.send(sourceId,
                "You don't have enough energy for " + abilityId.replace(/_/g, " ") + ".\r\n");
        } else if (reason === "cooldown") {
            tapestry.world.send(sourceId,
                "You aren't ready to " + abilityId.replace(/_/g, " ") + " yet.\r\n");
        } else if (reason === "no_proficiency") {
            tapestry.world.send(sourceId,
                "You don't know how to " + abilityId.replace(/_/g, " ") + ".\r\n");
        } else if (reason === "not_in_combat") {
            tapestry.world.send(sourceId,
                "You aren't in combat.\r\n");
        }
    }
});
