tapestry.commands.register({
    name: 'grant',
    description: 'Admin: award progression or trains to a player, npc, or item.',
    category: 'admin',
    admin: true,
    args: {
        entity: { type: 'keyword', required: true },
        ability: { type: 'keyword', required: true },
        value: { type: 'text', required: false }
    },
    handler: function(actor, resolved) {

        var entityKind = resolved.entity.toLowerCase();
        var abilityType = resolved.ability.toLowerCase();
        var valueStr = resolved.value || '';
        var valueParts = valueStr.length > 0 ? valueStr.split(' ') : [];

        // Reconstruct args array for dispatch: [kind, type, target, ...rest]
        var dispatchArgs = [entityKind, abilityType].concat(valueParts);
        tapestry.admin.grant.dispatch(actor.entityId, dispatchArgs);
    }
});

tapestry.admin.grant.register({
    kind: 'player',
    type: 'xp',
    help: 'grant player xp [target] [amount] [track] - grant XP (track defaults to combat)',
    handler: function(admin, target, args) {
        if (args.length < 1) {
            admin.send('Usage: grant player xp [target] [amount] [track]\r\n');
            return;
        }
        var amount = parseInt(args[0], 10);
        if (isNaN(amount) || amount <= 0) {
            admin.send('Amount must be a positive number.\r\n');
            return;
        }
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
        if (args.length < 1) {
            admin.send('Usage: grant player train [target] [amount]\r\n');
            return;
        }
        var amount = parseInt(args[0], 10);
        if (isNaN(amount) || amount < 1) {
            admin.send('Amount must be a positive integer.\r\n');
            return;
        }
        tapestry.training.grantTrains(target.id, amount);
        admin.send('Granted ' + amount + ' trains to ' + target.name + '.\r\n');
    }
});

tapestry.admin.grant.register({
    kind: 'player',
    type: 'gold',
    help: 'grant player gold [target] [amount] - add (or subtract) gold; negative clamps at 0',
    handler: function(admin, target, args) {
        if (args.length < 1) {
            admin.send('Usage: grant player gold [target] [amount]\r\n');
            return;
        }
        var amount = parseInt(args[0], 10);
        if (isNaN(amount)) {
            admin.send('Amount must be a number.\r\n');
            return;
        }
        tapestry.currency.addGold(target.id, amount, 'admin:grant');
        var total = tapestry.currency.getGold(target.id);
        admin.send(target.name + "'s gold is now " + total + ".\r\n");
    }
});
