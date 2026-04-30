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
        priority: 10,
        handler: function(player, args) {
            var restState = tapestry.rest.getRestState(player.entityId);
            if (restState === 'resting' || restState === 'sleeping') {
                player.send("You can't move while " + restState + ". Type 'wake' to stand up.\r\n");
                return;
            }
            if (tapestry.combat.isInCombat(player.entityId)) {
                player.send("You can't move while fighting! Type 'flee' to escape.\r\n");
                return;
            }
            var oldRoomId = player.roomId;

            var door = tapestry.doors.getDoor(oldRoomId, dir);
            if (door && door.isClosed) {
                player.send('The ' + door.name + ' is closed.\r\n');
                return;
            }

            var moved = tapestry.world.moveEntity(player.entityId, dir);
            if (moved) {
                var newRoomId = tapestry.world.getEntityRoomId(player.entityId);
                tapestry.world.sendRoomDescription(player.entityId);
                tapestry.world.triggerDisposition(player.entityId);
                tapestry.world.sendToRoomExceptSleeping(
                    oldRoomId,
                    player.entityId,
                    player.name + ' leaves ' + dir + '.\r\n'
                );
                tapestry.world.sendToRoomExceptSleeping(
                    newRoomId,
                    player.entityId,
                    player.name + ' arrives from ' + opposites[dir] + '.\r\n'
                );
                tapestry.events.publish('player.direction.moved', {
                    entityId: player.entityId,
                    leaderName: player.name,
                    direction: dir,
                    fromRoom: oldRoomId,
                    toRoom: newRoomId,
                    arrivalFrom: opposites[dir]
                });
            } else {
                player.send('You cannot go that way.\r\n');
                tapestry.events.publish('player.move.failed', {
                    entityId: player.entityId,
                    direction: dir,
                    roomId: player.roomId
                });
            }
        }
    });
});
