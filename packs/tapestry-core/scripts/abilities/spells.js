// packs/tapestry-core/scripts/abilities/spells.js

function findCastTarget(player, targetName) {
    var entities = tapestry.world.getEntitiesInRoom(player.roomId, "npc");
    var lowerName = targetName.toLowerCase();
    for (var i = 0; i < entities.length; i++) {
        if (entities[i].name.toLowerCase().indexOf(lowerName) !== -1) {
            return entities[i].id;
        }
    }
    return null;
}

function resolveSpellName(entityId, args) {
    // Returns { id: spellId, wordsConsumed: N } or null

    // First try exact multi-word match (longest first)
    for (var len = args.length; len >= 1; len--) {
        var candidate = [];
        for (var i = 0; i < len; i++) {
            candidate.push(args[i].toLowerCase());
        }
        var spellId = candidate.join("_");
        var prof = tapestry.abilities.getProficiency(entityId, spellId);
        if (prof) {
            return { id: spellId, wordsConsumed: len };
        }
    }

    // Then try prefix match against spell IDs and display names
    // Try consuming progressively fewer words as the prefix
    var learned = tapestry.abilities.getLearnedAbilities(entityId);
    for (var tryLen = args.length; tryLen >= 1; tryLen--) {
        var input = [];
        for (var k = 0; k < tryLen; k++) {
            input.push(args[k].toLowerCase());
        }
        var inputStr = input.join(" ");
        var inputId = input.join("_");

        for (var j = 0; j < learned.length; j++) {
            var def = tapestry.abilities.getDefinition(learned[j].id);
            if (def && def.category === "spell") {
                if (learned[j].id.indexOf(inputId) === 0 ||
                    def.name.toLowerCase().indexOf(inputStr) === 0) {
                    return { id: learned[j].id, wordsConsumed: tryLen };
                }
            }
        }
    }

    return null;
}

tapestry.abilities.register({
    id: "fireball",
    name: "Fireball",
    type: "active",
    category: "spell",
    resource_cost: 25,
    proficiency_gain_chance: 0.04,
    can_target: ["npc"],
    metadata: { damage_dice: "3d8+10", damage_type: "fire" },
    handler: function(caster, target, context) {
        var prof = tapestry.abilities.getProficiency(caster.entityId, "fireball");
        var baseDamage = tapestry.dice.roll("3d8+10");
        var profScale = prof / 100;
        var channelBonus = caster.stats.channeling_damage || 0;
        var totalDamage = Math.floor(baseDamage * profScale) + channelBonus;

        var protection = target.stats.channeling_protection || 0;
        totalDamage = Math.max(1, totalDamage - protection);

        tapestry.combat.applyDamage(target.entityId, totalDamage, "fire");

        var dv = tapestry.combat.formatDamageVerb(totalDamage);
        tapestry.world.send(caster.entityId,
            "Your Fireball " + dv + " " + target.name + "!\r\n");
        tapestry.world.send(target.entityId,
            caster.name + "'s Fireball " + dv + " you!\r\n");
        if (caster.roomId) {
            tapestry.world.sendToRoomExceptMany(caster.roomId, [caster.entityId, target.entityId],
                caster.name + "'s Fireball " + dv + " " + target.name + "!\r\n");
        }
    }
});

tapestry.abilities.register({
    id: "cure_light",
    name: "Cure Light",
    type: "active",
    category: "spell",
    resource_cost: 15,
    proficiency_gain_chance: 0.05,
    can_target: ["self", "player"],
    metadata: { heal_dice: "2d8+5" },
    handler: function(caster, target, context) {
        var prof = tapestry.abilities.getProficiency(caster.entityId, "cure_light");
        var baseHeal = tapestry.dice.roll("2d8+5");
        var profScale = prof / 100;
        var healAmount = Math.max(1, Math.floor(baseHeal * profScale));

        if (tapestry.world.hasTag(target.entityId, "no_heal")) {
            tapestry.world.send(caster.entityId,
                "A dark force prevents your healing from reaching " + target.name + ".\r\n");
            return;
        }

        tapestry.stats.addVital(target.entityId, "hp", healAmount);
        if (target.entityId === caster.entityId) {
            tapestry.world.send(caster.entityId,
                "Your cure light heals you for " + healAmount + " hp.\r\n");
        } else {
            tapestry.world.send(caster.entityId,
                "Your cure light heals " + target.name + " for " + healAmount + " hp.\r\n");
            tapestry.world.send(target.entityId,
                caster.name + "'s cure light heals you for " + healAmount + " hp.\r\n");
        }
    }
});

tapestry.abilities.register({
    id: "blindness",
    name: "Blindness",
    type: "active",
    category: "spell",
    resource_cost: 20,
    proficiency_gain_chance: 0.03,
    can_target: ["npc", "player"],
    metadata: {},
    effect: {
        effect_id: "blindness",
        duration: 30,
        flags: ["is_blind"],
        stat_modifiers: [{ stat: "Dexterity", value: -4 }]
    },
    handler: function(caster, target, context) {
        if (tapestry.combat.savingThrow(target.entityId, "spell")) {
            tapestry.world.send(caster.entityId,
                target.name + " resists your blindness!\r\n");
            tapestry.world.send(target.entityId,
                "You resist " + caster.name + "'s blindness!\r\n");
            return false;
        }
        tapestry.world.send(caster.entityId,
            target.name + " is struck blind!\r\n");
        tapestry.world.send(target.entityId,
            "You are struck blind by " + caster.name + "!\r\n");
    }
});

tapestry.abilities.register({
    id: "shield",
    name: "Shield",
    type: "active",
    category: "spell",
    resource_cost: 30,
    proficiency_gain_chance: 0.03,
    can_target: ["self", "player"],
    metadata: {},
    effect: {
        effect_id: "shield",
        duration: 60,
        flags: ["shield"],
        stat_modifiers: []
    },
    handler: function(caster, target, context) {
        tapestry.world.send(target.entityId,
            "A shimmering magical shield surrounds you.\r\n");
        if (target.roomId) {
            tapestry.world.sendToRoomExcept(target.roomId, target.entityId,
                "A shimmering magical shield surrounds " + target.name + ".\r\n");
        }
    }
});

tapestry.abilities.register({
    id: "poison",
    name: "Poison",
    type: "active",
    category: "spell",
    resource_cost: 20,
    proficiency_gain_chance: 0.03,
    can_target: ["npc", "player"],
    metadata: {},
    effect: {
        effect_id: "poisoned",
        duration: 40,
        flags: ["is_poisoned", "no_heal"],
        stat_modifiers: [{ stat: "Strength", value: -3 }]
    },
    handler: function(caster, target, context) {
        if (tapestry.combat.savingThrow(target.entityId, "spell")) {
            tapestry.world.send(caster.entityId,
                target.name + " resists your poison!\r\n");
            tapestry.world.send(target.entityId,
                "You resist " + caster.name + "'s poison!\r\n");
            return false;
        }
        tapestry.world.send(caster.entityId,
            target.name + " is poisoned!\r\n");
        tapestry.world.send(target.entityId,
            "You feel poison coursing through your veins!\r\n");
    }
});

// --- Cast command ---
tapestry.commands.register({
    name: "cast",
    description: 'Cast a spell.',
    aliases: ["c"],
    handler: function(player, args) {
        if (args.length === 0) {
            player.send("Cast what?\r\n");
            return;
        }

        // Try to match the spell name — supports multi-word and prefix matching
        var match = resolveSpellName(player.entityId, args);
        if (!match) {
            player.send("You don't know that spell.\r\n");
            return;
        }

        var spellId = match.id;
        var remainingArgs = args.slice(match.wordsConsumed);

        // Check can_target to decide self-targeting behavior
        var spellDef = tapestry.abilities.getDefinition(spellId);
        var canTarget = (spellDef && spellDef.can_target) ? spellDef.can_target : [];
        var canSelfTarget = false;
        for (var i = 0; i < canTarget.length; i++) {
            if (canTarget[i] === "self") {
                canSelfTarget = true;
                break;
            }
        }

        // Determine target
        var targetId = null;
        if (remainingArgs.length > 0) {
            var targetName = remainingArgs.join(" ");
            targetId = findCastTarget(player, targetName);
            if (!targetId) {
                player.send("You don't see that here.\r\n");
                return;
            }
        } else if (tapestry.combat.isInCombat(player.entityId)) {
            var canTargetEnemy = false;
            for (var j = 0; j < canTarget.length; j++) {
                if (canTarget[j] === "npc") {
                    canTargetEnemy = true;
                    break;
                }
            }
            if (canTargetEnemy) {
                var combatants = tapestry.combat.getCombatants(player.entityId);
                if (combatants.length > 0) {
                    targetId = combatants[0];
                }
            }
        }

        if (!targetId) {
            if (canSelfTarget) {
                targetId = player.entityId;
            } else {
                var name = spellDef ? spellDef.name : spellId;
                player.send("Cast " + name + " on whom?\r\n");
                return;
            }
        }

        // If targeting someone else and not in combat, engage
        if (targetId !== player.entityId && !tapestry.combat.isInCombat(player.entityId)) {
            var result = tapestry.combat.engage(player.entityId, targetId);
            if (result !== "ok") {
                player.send("You can't attack that.\r\n");
                return;
            }
        }

        tapestry.abilities.queue(player.entityId, spellId, targetId);
    }
});
