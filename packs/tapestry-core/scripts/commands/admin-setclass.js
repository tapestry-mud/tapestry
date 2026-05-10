tapestry.commands.register({
    name: 'setclass',
    description: 'Assign a class to a player and grant their level-1 abilities.',
    category: 'admin',
    admin: true,
    args: {
        entity: { type: 'keyword', required: true },
        classId: { type: 'keyword', required: true }
    },
    handler: function(actor, resolved) {
        var entityName = resolved.entity;
        var classId = resolved.classId.toLowerCase();

        var players = tapestry.world.getOnlinePlayers();
        var lowerName = entityName.toLowerCase();
        var target = null;

        for (var i = 0; i < players.length; i++) {
            if (players[i].name.toLowerCase() === lowerName) {
                target = { id: players[i].id, name: players[i].name };
                break;
            }
        }

        if (!target) {
            actor.send("Player '" + entityName + "' not found.\r\n");
            return;
        }

        tapestry.classes.setClass(target.id, classId);
        actor.send(target.name + ' is now a ' + classId + '.\r\n');
    }
});
