tapestry.commands.register({
    name: 'setclass',
    admin: true,
    description: 'Admin: assign a class to a player and grant their level-1 abilities.',
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (args.length < 2) {
            player.send('Usage: setclass [player] [class-id]\r\n');
            return;
        }
        var targetName = args[0].toLowerCase();
        var classId = args[1].toLowerCase();
        var players = tapestry.world.getOnlinePlayers();
        var target = null;
        for (var i = 0; i < players.length; i++) {
            if (players[i].name.toLowerCase() === targetName) {
                target = players[i];
                break;
            }
        }
        if (!target) {
            player.send('Player ' + args[0] + ' not found.\r\n');
            return;
        }
        tapestry.classes.setClass(target.id, classId);
        player.send(target.name + " is now a " + classId + ".\r\n");
    }
});
