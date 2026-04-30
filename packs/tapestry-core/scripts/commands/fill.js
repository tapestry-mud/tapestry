// packs/tapestry-core/scripts/commands/fill.js
tapestry.commands.register({
    name: 'fill',
    description: 'Fill a container from a source.',
    handler: function(player, args) {
        if (args.length < 2) {
            player.send('Fill what from what? Usage: fill [container] [source]\r\n');
            return;
        }
        var targetKeyword = args[0];
        var sourceKeyword = args.slice(1).join(' ');

        var result = tapestry.inventory.fillItem(player.entityId, targetKeyword, sourceKeyword);
        if (!result) {
            player.send("You can't do that.\r\n");
            return;
        }
        if (result.success) {
            player.send('You fill ' + result.targetName + ' from ' + result.sourceName + '.\r\n');
            player.sendToRoom(player.name + ' fills ' + result.targetName + '.\r\n');
        } else if (result.reason === 'target_not_found') {
            player.send("You aren't carrying that.\r\n");
        } else if (result.reason === 'source_not_found') {
            player.send("You don't see a source for that here.\r\n");
        } else if (result.reason === 'not_fillable') {
            player.send("You can't fill that.\r\n");
        } else if (result.reason === 'mixed_liquids') {
            player.send("You can't mix liquids.\r\n");
        } else if (result.reason === 'source_empty') {
            player.send(result.sourceName + " has dried up.\r\n");
        } else {
            player.send("You can't do that.\r\n");
        }
    }
});
