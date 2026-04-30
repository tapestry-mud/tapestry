tapestry.commands.register({
    name: 'settrainable',
    admin: true,
    description: 'Runtime debug: toggle a stat in/out of the trainable list.',
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        if (args.length < 2) {
            player.send('Usage: settrainable [stat] <true|false>\r\n');
            return;
        }
        var stat = args[0].toLowerCase();
        var enabled = args[1].toLowerCase() === 'true';
        tapestry.training.setTrainable(stat, enabled);
        player.send((enabled ? 'Enabled' : 'Disabled') + ' training for ' + stat + '.\r\n');
    }
});
