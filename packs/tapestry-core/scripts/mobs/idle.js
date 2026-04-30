tapestry.events.on("mob.ai.tick", function(event) {
    var mob = event.data;

    if (tapestry.combat.isInCombat(mob.entityId)) {
        return;
    }

    var props = tapestry.mobs.getProperties(mob.entityId);
    if (!props || !props.idle_commands || props.idle_commands.length === 0) {
        return;
    }

    var ticksSinceIdle = tapestry.mobs.getTicksSinceLastAction(mob.entityId);
    if (ticksSinceIdle < (props.idle_interval || 30)) {
        return;
    }

    if (Math.random() > (props.idle_chance || 0.3)) {
        return;
    }

    tapestry.mobs.recordAction(mob.entityId);

    var commands = props.idle_commands;
    var command = commands[Math.floor(Math.random() * commands.length)];
    tapestry.mobs.command(mob.entityId, command);
});
