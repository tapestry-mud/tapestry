// packs/tapestry-core/scripts/abilities/rescue.js

tapestry.abilities.register({
    id: "rescue",
    name: "Rescue",
    type: "active",
    category: "skill",
    resource_cost: 0,
    proficiency_gain_chance: 0.05,
    can_target: ["player"],
    metadata: {}
});

tapestry.commands.register({
    name: "rescue",
    description: 'Rescue an ally from combat.',
    priority: 1,
    visibleTo: function(entity) {
        var prof = tapestry.abilities.getProficiency(entity.entityId, "rescue");
        return !!prof && prof > 0;
    },
    handler: function(player, args) {
        var prof = tapestry.abilities.getProficiency(player.entityId, "rescue");
        if (!prof || prof <= 0) {
            player.send("You don't know how to Rescue.\r\n");
            return;
        }

        if (!args || args.length === 0) {
            player.send("Rescue whom?\r\n");
            return;
        }

        var cooldownUntil = tapestry.world.getProperty(player.entityId, "rescue_cooldown_until") || 0;
        if (Date.now() < cooldownUntil) {
            player.send("You aren't ready to rescue yet.\r\n");
            return;
        }

        var targetName = args[0].toLowerCase();
        var playersInRoom = tapestry.world.getEntitiesInRoom(player.roomId, "player");
        var target = null;
        for (var i = 0; i < playersInRoom.length; i++) {
            if (playersInRoom[i].name.toLowerCase().indexOf(targetName) === 0 &&
                playersInRoom[i].id !== player.entityId) {
                target = playersInRoom[i];
                break;
            }
        }

        if (!target) {
            player.send("You don't see that person here.\r\n");
            return;
        }

        var rescuerGroupId = tapestry.world.getProperty(player.entityId, "group_id");
        var targetGroupId = tapestry.world.getProperty(target.id, "group_id");
        if (!rescuerGroupId || rescuerGroupId !== targetGroupId) {
            player.send("You can only rescue a group member.\r\n");
            return;
        }

        if (!tapestry.combat.isInCombat(target.id)) {
            player.send(target.name + " is not in combat.\r\n");
            return;
        }

        var chance = (prof >= 100) ? 100 : Math.max(10, prof * 0.8);
        var roll = Math.random() * 100;

        tapestry.world.setProperty(player.entityId, "rescue_cooldown_until", Date.now() + 4000);

        if (roll >= chance) {
            player.send("You fail to rescue " + target.name + ".\r\n");
            return;
        }

        var npcsInRoom = tapestry.world.getEntitiesInRoom(player.roomId, "npc");
        var targetCombatants = tapestry.combat.getCombatants(target.id);
        var redirected = false;

        for (var j = 0; j < targetCombatants.length; j++) {
            var attackerId = targetCombatants[j];
            var isNpcAttacker = false;
            for (var k = 0; k < npcsInRoom.length; k++) {
                if (npcsInRoom[k].id === attackerId) {
                    isNpcAttacker = true;
                    break;
                }
            }
            if (isNpcAttacker) {
                tapestry.combat.setPrimaryTarget(attackerId, player.entityId);
                tapestry.combat.engage(player.entityId, attackerId);
                redirected = true;
            }
        }

        if (!redirected) {
            player.send("You fail to rescue " + target.name + ".\r\n");
            return;
        }

        player.send("You rescue " + target.name + "!\r\n");
        tapestry.world.send(target.id, player.name + " rescues you!\r\n");

        tapestry.events.publish("rescue.success", {
            rescuerId: player.entityId,
            targetId: target.id
        });
    }
});
