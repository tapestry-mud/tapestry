// --- kill / attack command ---
tapestry.commands.register({
    name: "kill",
    aliases: ["attack"],
    description: "Attack a target to initiate combat.",
    handler: function(player, args) {
        var restState = tapestry.rest.getRestState(player.entityId);
        if (restState === 'resting' || restState === 'sleeping') {
            player.send("You can't attack while " + restState + ". Type 'wake' to stand up.\r\n");
            return;
        }
        if (!args || args.length === 0) {
            player.send("Kill what?\r\n");
            return;
        }

        var raw = args.join(" ").toLowerCase();
        var entities = tapestry.world.getEntitiesInRoom(player.roomId, "npc");
        if (!entities || entities.length === 0) {
            player.send("There is nothing here to fight.\r\n");
            return;
        }

        // Parse optional index prefix: "2.goblin" targets the second goblin
        var targetKeyword = raw;
        var targetIndex = 1;
        var dotPos = raw.indexOf('.');
        if (dotPos > 0) {
            var maybeIndex = parseInt(raw.substring(0, dotPos), 10);
            if (!isNaN(maybeIndex)) {
                targetIndex = maybeIndex;
                targetKeyword = raw.substring(dotPos + 1);
            }
        }

        var matches = [];
        for (var i = 0; i < entities.length; i++) {
            var e = entities[i];
            if (e.name.toLowerCase().indexOf(targetKeyword) >= 0) {
                matches.push(e);
            }
        }
        var target = targetIndex <= matches.length ? matches[targetIndex - 1] : null;

        if (!target) {
            player.send("You don't see '" + targetKeyword + "' here.\r\n");
            return;
        }

        var result = tapestry.combat.engage(player.entityId, target.id);
        if (result === "no-kill") {
            player.send("You can't attack " + target.name + ".\r\n");
        } else if (result === "safe-room") {
            player.send("You can't fight here.\r\n");
        } else if (result === "already-fighting") {
            player.send("You're already fighting " + target.name + "!\r\n");
        } else if (result === "flee-cooldown") {
            player.send("You're too winded from fleeing to attack right now.\r\n");
        } else if (result === "ok") {
            player.send("You attack " + target.name + "!\r\n");
        }
    }
});

// --- flee command ---
tapestry.commands.register({
    name: "flee",
    description: "Attempt to flee from combat.",
    handler: function(player, args) {
        if (!tapestry.combat.isInCombat(player.entityId)) {
            player.send("You're not in combat.\r\n");
            return;
        }

        var result = tapestry.combat.flee(player.entityId);
        // Events handle the output messages
    }
});

// --- wimpy command ---
tapestry.commands.register({
    name: "wimpy",
    description: "Set HP threshold to automatically flee combat.",
    handler: function(player, args) {
        if (!args || args.length === 0) {
            var current = tapestry.world.getProperty(player.entityId, "wimpy_threshold") || 0;
            player.send("Your wimpy is set to " + current + "%.\r\n");
            return;
        }

        var value = parseInt(args[0], 10);
        if (isNaN(value) || value < 0 || value > 50) {
            player.send("Wimpy must be between 0 and 50.\r\n");
            return;
        }

        tapestry.world.setProperty(player.entityId, "wimpy_threshold", value);
        if (value === 0) {
            player.send("Wimpy disabled. You will fight to the death.\r\n");
        } else {
            player.send("Wimpy set to " + value + "%. You will flee when HP drops below " + value + "%.\r\n");
        }
    }
});

// --- consider command ---
tapestry.commands.register({
    name: "consider",
    aliases: ["con"],
    description: "Assess how dangerous a target is compared to you.",
    handler: function(player, args) {
        if (!args || args.length === 0) {
            player.send("Consider what?\r\n");
            return;
        }

        var targetKeyword = args.join(" ").toLowerCase();
        var entities = tapestry.world.getEntitiesInRoom(player.roomId, "npc");
        if (!entities || entities.length === 0) {
            player.send("There is nothing here.\r\n");
            return;
        }

        var target = null;
        for (var i = 0; i < entities.length; i++) {
            var e = entities[i];
            if (e.name.toLowerCase().indexOf(targetKeyword) >= 0) {
                target = e;
                break;
            }
        }

        if (!target) {
            player.send("You don't see '" + targetKeyword + "' here.\r\n");
            return;
        }

        var playerLevel = tapestry.world.getProperty(player.entityId, "level") || 1;
        var targetLevel = tapestry.world.getProperty(target.id, "level") || 1;
        var delta = playerLevel - targetLevel;

        var message;
        if (delta >= 5) {
            message = "You could squash " + target.name + " like a bug.";
        } else if (delta >= 2) {
            message = target.name + " should be manageable.";
        } else if (delta >= -1) {
            message = target.name + " would be an even fight.";
        } else if (delta >= -4) {
            message = target.name + " looks dangerous...";
        } else {
            message = target.name + " would be certain death.";
        }

        player.send(message + "\r\n");
    }
});
