function resolvePlayerByName(name) {
    var players = tapestry.world.getOnlinePlayers();
    var lowerName = name.toLowerCase();
    for (var i = 0; i < players.length; i++) {
        if (players[i].name.toLowerCase() === lowerName) {
            return players[i];
        }
    }
    return null;
}

tapestry.commands.register({
    name: 'forget',
    admin: true,
    description: 'Remove an ability from yourself or another player.',
    priority: 0,
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (!args || args.length < 1) {
            player.send('Usage: forget [ability-id] [player]\r\n');
            return;
        }

        var abilityId = args[0].toLowerCase();
        var target = { id: player.entityId, name: player.name };

        if (args.length >= 2) {
            var targetName = args[1];
            if (targetName.toLowerCase() !== 'self' && targetName.toLowerCase() !== player.name.toLowerCase()) {
                var resolved = resolvePlayerByName(targetName);
                if (!resolved) {
                    player.send("Player '" + targetName + "' not found.\r\n");
                    return;
                }
                target = resolved;
            }
        }

        tapestry.abilities.forget(target.id, abilityId);
        player.send('Removed ' + abilityId + ' from ' + target.name + '.\r\n');
    }
});
