tapestry.commands.register({
    name: 'grant',
    admin: true,
    description: 'Admin: award progression or trains. Type `grant ?` for help.',
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        tapestry.admin.grant.dispatch(player.entityId, args);
    }
});

tapestry.admin.grant.register({
    kind: 'player',
    type: 'xp',
    help: 'grant player xp [target] [amount] [track] - grant XP (track defaults to combat)',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: grant player xp [target] [amount] [track]\r\n'); return; }
        var amount = parseInt(args[0], 10);
        if (isNaN(amount) || amount <= 0) { admin.send('Amount must be a positive number.\r\n'); return; }
        var track = args[1] || 'combat';
        tapestry.progression.grant(target.id, amount, track, 'admin');
        admin.send('Granted ' + amount + " XP to " + target.name + " on track '" + track + "'.\r\n");
    }
});

tapestry.admin.grant.register({
    kind: 'player',
    type: 'train',
    help: 'grant player train [target] [amount] - grant training sessions',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: grant player train [target] [amount]\r\n'); return; }
        var amount = parseInt(args[0], 10);
        if (isNaN(amount) || amount < 1) { admin.send('Amount must be a positive integer.\r\n'); return; }
        tapestry.training.grantTrains(target.id, amount);
        admin.send('Granted ' + amount + ' trains to ' + target.name + '.\r\n');
    }
});

tapestry.admin.grant.register({
    kind: 'player',
    type: 'gold',
    help: 'grant player gold [target] [amount] - add (or subtract) gold; negative clamps at 0',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: grant player gold [target] [amount]\r\n'); return; }
        var amount = parseInt(args[0], 10);
        if (isNaN(amount)) { admin.send('Amount must be a number.\r\n'); return; }
        tapestry.currency.addGold(target.id, amount, 'admin:grant');
        var total = tapestry.currency.getGold(target.id);
        admin.send(target.name + "'s gold is now " + total + ".\r\n");
    }
});
