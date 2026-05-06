// packs/tapestry-core/scripts/abilities/passives.js

// --- Passive Skills (binary check) ---

tapestry.abilities.register({
    id: "dodge",
    name: "Dodge",
    type: "passive",
    category: "skill",
    proficiency_gain_chance: 0.04,
    max_chance: 40,
    metadata: { passive_mode: "binary", hook: "defensive_check" }
});

tapestry.abilities.register({
    id: "parry",
    name: "Parry",
    type: "passive",
    category: "skill",
    proficiency_gain_chance: 0.04,
    max_chance: 40,
    metadata: { passive_mode: "binary", hook: "defensive_check" }
});

tapestry.abilities.register({
    id: "second_attack",
    name: "Second Attack",
    type: "passive",
    category: "skill",
    proficiency_gain_chance: 0.03,
    max_chance: 75,
    metadata: { passive_mode: "binary", hook: "extra_attack" }
});

// --- Passive Skills (scaling) ---

tapestry.abilities.register({
    id: "enhanced_damage",
    name: "Enhanced Damage",
    type: "passive",
    category: "skill",
    proficiency_gain_chance: 0.03,
    max_chance: 100,
    metadata: { passive_mode: "scaling", hook: "stat_modifier", stat: "damage_roll", max_bonus: 50 }
});

tapestry.abilities.register({
    id: "fast_healing",
    name: "Fast Healing",
    type: "passive",
    category: "skill",
    proficiency_gain_chance: 0.03,
    max_chance: 100,
    metadata: { passive_mode: "scaling", hook: "regen_modifier", stat: "regen_hp", max_bonus: 10 }
});

tapestry.abilities.register({
    id: "second_cast",
    name: "Second Cast",
    type: "passive",
    category: "spell",
    proficiency_gain_chance: 0.02,
    max_chance: 65,
    metadata: { passive_mode: "binary", hook: "extra_attack" }
});

// --- Combat evade output ---
tapestry.events.on("combat.evade", function(event) {
    var data = event.data || {};
    var defenderName = data.defenderName || "Someone";
    var attackerName = data.attackerName || "something";
    var abilityId = data.abilityId || "dodge";
    var abilityName = tapestry.abilities.getDisplayName(abilityId);

    var roomId = event.roomId;
    if (roomId && event.sourceEntityId && event.targetEntityId) {
        tapestry.world.send(event.sourceEntityId,
            "<combat_evade>You " + abilityName + " " + attackerName + "'s attack!</combat_evade>\r\n");
        tapestry.world.send(event.targetEntityId,
            "<combat_evade>" + defenderName + " " + abilityName + "s your attack!</combat_evade>\r\n");
        tapestry.world.sendToRoomExceptMany(roomId, [event.sourceEntityId, event.targetEntityId],
            "<combat_evade>" + defenderName + " " + abilityName + "s " + attackerName + "'s attack!</combat_evade>\r\n");
    }
});
