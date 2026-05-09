var directions = [
    { name: 'north', alias: 'n' },
    { name: 'south', alias: 's' },
    { name: 'east',  alias: 'e' },
    { name: 'west',  alias: 'w' },
    { name: 'up',    alias: 'u' },
    { name: 'down',  alias: 'd' }
];

var opposites = {
    north: 'the south',
    south: 'the north',
    east: 'the west',
    west: 'the east',
    up: 'below',
    down: 'above'
};

directions.forEach(function(entry) {
    var dir = entry.name;
    tapestry.commands.register({
        name: dir,
        aliases: [entry.alias],
        description: 'Move ' + dir + '.',
        category: 'world',
        roles: ['player'],
        args: {},
        priority: 10,
        handler: function(actor, resolved) {
            var restState = tapestry.rest.getRestState(actor.entityId);
            if (restState === 'resting' || restState === 'sleeping') {
                actor.send("You can't move while " + restState + ". Type 'wake' to stand up.\r\n");
                return;
            }
            if (tapestry.combat.isInCombat(actor.entityId)) {
                actor.send("You can't move while fighting! Type 'flee' to escape.\r\n");
                return;
            }
            var oldRoomId = actor.roomId;

            var door = tapestry.doors.getDoor(oldRoomId, dir);
            if (door && door.isClosed) {
                actor.send('The ' + door.name + ' is closed.\r\n');
                return;
            }

            var moved = tapestry.world.moveEntity(actor.entityId, dir);
            if (moved) {
                var newRoomId = tapestry.world.getEntityRoomId(actor.entityId);
                tapestry.world.sendRoomDescription(actor.entityId);
                tapestry.world.triggerDisposition(actor.entityId);
                tapestry.world.sendToRoomExceptSleeping(
                    oldRoomId,
                    actor.entityId,
                    actor.name + ' leaves ' + dir + '.\r\n'
                );
                tapestry.world.sendToRoomExceptSleeping(
                    newRoomId,
                    actor.entityId,
                    actor.name + ' arrives from ' + opposites[dir] + '.\r\n'
                );
                tapestry.events.publish('player.direction.moved', {
                    entityId: actor.entityId,
                    leaderName: actor.name,
                    direction: dir,
                    fromRoom: oldRoomId,
                    toRoom: newRoomId,
                    arrivalFrom: opposites[dir]
                });
            } else {
                actor.send('You cannot go that way.\r\n');
                tapestry.events.publish('player.move.failed', {
                    entityId: actor.entityId,
                    direction: dir,
                    roomId: actor.roomId
                });
            }
        }
    });
});
