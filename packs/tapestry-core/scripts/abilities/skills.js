// packs/tapestry-core/scripts/abilities/skills.js

tapestry.abilities.register({
    id: "battle_stance",
    name: "Battle Stance",
    type: "active",
    category: "skill",
    resource_cost: 15,
    proficiency_gain_chance: 0.04,
    can_target: ["self"],
    metadata: {},
    effect: {
        effect_id: "battle_stance",
        duration: 25,
        flags: ["battle_stance"],
        stat_modifiers: [{ stat: "Strength", value: 3 }]
    },
    handler: function(user, target, context) {
        tapestry.world.send(user.entityId,
            "You settle into a fierce battle stance.\r\n");
        if (user.roomId) {
            tapestry.world.sendToRoomExcept(user.roomId, user.entityId,
                user.name + " settles into a fierce battle stance.\r\n");
        }
    }
});


tapestry.abilities.register({
    id: "kick",
    name: "Kick",
    type: "active",
    category: "skill",
    resource_cost: 10,
    proficiency_gain_chance: 0.05,
    can_target: ["npc"],
    metadata: { damage_dice: "2d6+4", damage_type: "bash" },
    handler: function(user, target, context) {
        var prof = tapestry.abilities.getProficiency(user.entityId, "kick");
        var baseDamage = tapestry.dice.roll("2d6+4");
        var profScale = prof / 100;
        var damageBonus = user.stats.damage_roll || 0;
        var totalDamage = Math.floor(baseDamage * profScale) + damageBonus;
        totalDamage = tapestry.combat.applyAC(target.entityId, totalDamage, "bash");

        tapestry.combat.applyDamage(target.entityId, totalDamage, "bash");

        var dv = tapestry.combat.formatDamageVerb(totalDamage);
        tapestry.world.send(user.entityId,
            "Your kick " + dv + " " + target.name + "!\r\n");
        tapestry.world.send(target.entityId,
            user.name + "'s kick " + dv + " you!\r\n");
        if (user.roomId) {
            tapestry.world.sendToRoomExceptMany(user.roomId, [user.entityId, target.entityId],
                user.name + "'s kick " + dv + " " + target.name + "!\r\n");
        }
    }
});

tapestry.abilities.register({
    id: "bash",
    name: "Bash",
    type: "active",
    category: "skill",
    resource_cost: 15,
    proficiency_gain_chance: 0.04,
    can_target: ["npc"],
    metadata: { damage_dice: "2d8+6", damage_type: "bash" },
    handler: function(user, target, context) {
        var prof = tapestry.abilities.getProficiency(user.entityId, "bash");
        var baseDamage = tapestry.dice.roll("2d8+6");
        var profScale = prof / 100;
        var damageBonus = user.stats.damage_roll || 0;
        var totalDamage = Math.floor(baseDamage * profScale) + damageBonus;
        totalDamage = tapestry.combat.applyAC(target.entityId, totalDamage, "bash");

        tapestry.combat.applyDamage(target.entityId, totalDamage, "bash");

        tapestry.effects.apply(target.entityId, {
            id: "stunned",
            duration: 1,
            flags: ["stunned"]
        });

        var dv = tapestry.combat.formatDamageVerb(totalDamage);
        tapestry.world.send(user.entityId,
            "Your bash " + dv + " " + target.name + "!\r\n");
        tapestry.world.send(target.entityId,
            user.name + "'s bash " + dv + " you! You are stunned!\r\n");
        if (user.roomId) {
            tapestry.world.sendToRoomExceptMany(user.roomId, [user.entityId, target.entityId],
                user.name + "'s bash " + dv + " " + target.name + "!\r\n");
        }
    }
});

