// Stationary — does nothing. Hook exists for future flavor (emotes, idle text).
tapestry.mobs.registerBehavior("stationary", function(mob) {
    // intentionally empty
});

// Wander — move to a random adjacent room within boundary.
tapestry.mobs.registerBehavior("wander", function(mob) {
    if (tapestry.combat.isInCombat(mob.entityId)) {
        return;
    }

    var props = tapestry.mobs.getProperties(mob.entityId);
    var ticksSinceMove = tapestry.mobs.getTicksSinceLastAction(mob.entityId);

    if (ticksSinceMove < (props.wander_interval || 30)) {
        return;
    }

    tapestry.mobs.recordAction(mob.entityId);

    if (Math.random() > (props.wander_chance || 0.3)) {
        return;
    }

    var exits = tapestry.world.getRoomExits(mob.entityId);
    if (!exits || exits.length === 0) {
        return;
    }

    var exit = exits[Math.floor(Math.random() * exits.length)];
    var targetRoom = tapestry.world.getExitTarget(mob.roomId, exit);

    if (!targetRoom) {
        return;
    }

    var targetTags = tapestry.world.getRoomTags(targetRoom);
    if (targetTags && targetTags.indexOf("no_wander") >= 0) {
        return;
    }

    var boundary = props.wander_boundary || "area";
    if (boundary === "area" && !tapestry.world.sameArea(mob.roomId, targetRoom)) {
        return;
    }

    var oldRoom = mob.roomId;
    tapestry.world.sendToRoom(oldRoom, mob.name + " leaves " + exit + ".\r\n");
    tapestry.world.moveEntity(mob.entityId, exit);
    var newRoom = tapestry.world.getEntityRoomId(mob.entityId);
    if (newRoom) {
        tapestry.world.sendToRoom(newRoom, mob.name + " arrives.\r\n");
    }
});

// Patrol — follow a defined waypoint route.
tapestry.mobs.registerBehavior("patrol", function(mob) {
    if (tapestry.combat.isInCombat(mob.entityId)) {
        return;
    }

    var props = tapestry.mobs.getProperties(mob.entityId);
    var route = props.patrol_route;
    if (!route || route.length < 2) {
        return;
    }

    var ticksSinceMove = tapestry.mobs.getTicksSinceLastAction(mob.entityId);
    if (ticksSinceMove < (props.patrol_interval || 30)) {
        return;
    }

    tapestry.mobs.recordAction(mob.entityId);

    if (Math.random() > (props.patrol_chance || 0.5)) {
        return;
    }

    var currentIndex = props._patrol_index || 0;
    var direction = props._patrol_direction || 1;

    var nextIndex = currentIndex + direction;
    if (nextIndex >= route.length || nextIndex < 0) {
        direction = -direction;
        nextIndex = currentIndex + direction;
    }

    var targetRoom = route[nextIndex];
    if (!targetRoom || targetRoom === mob.roomId) {
        tapestry.world.setProperty(mob.entityId, "_patrol_index", nextIndex);
        tapestry.world.setProperty(mob.entityId, "_patrol_direction", direction);
        return;
    }

    var exits = tapestry.world.getRoomExits(mob.entityId);
    if (!exits || exits.length === 0) {
        return;
    }

    var moveDir = null;
    for (var i = 0; i < exits.length; i++) {
        var candidate = tapestry.world.getExitTarget(mob.roomId, exits[i]);
        if (candidate === targetRoom) {
            moveDir = exits[i];
            break;
        }
    }

    if (!moveDir) {
        return;
    }

    var oldRoom = mob.roomId;
    tapestry.world.sendToRoom(oldRoom, mob.name + " leaves " + moveDir + ".\r\n");
    tapestry.world.moveEntity(mob.entityId, moveDir);
    var newRoom = tapestry.world.getEntityRoomId(mob.entityId);
    if (newRoom) {
        tapestry.world.sendToRoom(newRoom, mob.name + " arrives.\r\n");
    }

    tapestry.world.setProperty(mob.entityId, "_patrol_index", nextIndex);
    tapestry.world.setProperty(mob.entityId, "_patrol_direction", direction);
});

// Aggro — attacks first player in room. Checks room safety tags.
tapestry.mobs.registerBehavior("aggro", function(mob) {
    var players = tapestry.world.getEntitiesInRoom(mob.roomId, "player");
    if (!players || players.length === 0) {
        return;
    }

    var roomTags = tapestry.world.getRoomTags(mob.roomId);
    if (roomTags && (roomTags.indexOf("safe") >= 0 || roomTags.indexOf("no-combat") >= 0)) {
        return;
    }

    // Publishes event for combat system (Phase 3b) to handle
    tapestry.events.publish("mob.aggro", {
        attackerId: mob.entityId,
        targetId: players[0].id
    });
});
