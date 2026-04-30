tapestry.commands.register({
    name: 'set',
    admin: true,
    description: 'Admin: modify player/npc/item fields. Type `set ?` for help.',
    handler: function(player, args) {
        if (!player.hasTag('admin')) { player.send('Huh?\r\n'); return; }
        tapestry.admin.set.dispatch(player.entityId, args);
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'alignment',
    help: 'set player alignment [target] [value] - set alignment (-1000..1000)',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player alignment [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.alignment.set(target.id, value, 'admin_set');
        var actual = tapestry.alignment.get(target.id);
        var bucket = tapestry.alignment.bucket(target.id);
        admin.send(target.name + "'s alignment set to " + actual + " (" + bucket + ").\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'str',
    help: 'set player str [target] [value] - set base Strength',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player str [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.stats.setBase(target.id, 'strength', value);
        admin.send(target.name + "'s Strength set to " + value + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'int',
    help: 'set player int [target] [value] - set base Intelligence',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player int [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.stats.setBase(target.id, 'intelligence', value);
        admin.send(target.name + "'s Intelligence set to " + value + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'wis',
    help: 'set player wis [target] [value] - set base Wisdom',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player wis [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.stats.setBase(target.id, 'wisdom', value);
        admin.send(target.name + "'s Wisdom set to " + value + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'dex',
    help: 'set player dex [target] [value] - set base Dexterity',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player dex [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.stats.setBase(target.id, 'dexterity', value);
        admin.send(target.name + "'s Dexterity set to " + value + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'con',
    help: 'set player con [target] [value] - set base Constitution',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player con [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.stats.setBase(target.id, 'constitution', value);
        admin.send(target.name + "'s Constitution set to " + value + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'luck',
    help: 'set player luck [target] [value] - set base Luck',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player luck [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.stats.setBase(target.id, 'luck', value);
        admin.send(target.name + "'s Luck set to " + value + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'prof',
    help: 'set player prof [target] [ability] [value] - set proficiency %',
    handler: function(admin, target, args) {
        if (args.length < 2) { admin.send('Usage: set player prof [target] [ability] [value]\r\n'); return; }
        var abilityId = args[0];
        var value = parseInt(args[1], 10);
        if (isNaN(value)) { admin.send('Value must be a number.\r\n'); return; }
        tapestry.abilities.setProficiency(target.id, abilityId, value);
        admin.send(target.name + "'s " + abilityId + " proficiency set to " + value + "%.\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'cap',
    help: 'set player cap [target] [ability] [tier] - set tier cap (novice/apprentice/journeyman/master)',
    handler: function(admin, target, args) {
        if (args.length < 2) { admin.send('Usage: set player cap [target] [ability] <novice|apprentice|journeyman|master>\r\n'); return; }
        var abilityId = args[0];
        var tier = args[1].toLowerCase();
        if (!['novice', 'apprentice', 'journeyman', 'master'].includes(tier)) {
            admin.send('Invalid tier. Use: novice, apprentice, journeyman, master.\r\n');
            return;
        }
        tapestry.training.setCap(target.id, abilityId, tier);
        admin.send(target.name + "'s " + abilityId + " cap set to " + tier + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'npc',
    type: 'hp',
    help: 'set npc hp [mob] [value] - set mob current and max hp',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set npc hp [mob] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value) || value < 0) { admin.send('Value must be a non-negative number.\r\n'); return; }
        tapestry.admin.setEntityHp(target.id, value);
        admin.send(target.name + "'s hp set to " + value + " (hp and max hp).\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'item',
    type: 'dice',
    applies_to: ['weapon'],
    help: 'set item dice [item] [dice-string] - damage dice (e.g., 2d10+8)',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set item dice [item] [dice]\r\n'); return; }
        var diceStr = args[0];
        if (!/^\d+d\d+([+-]\d+)?$/.test(diceStr)) {
            admin.send("Invalid dice string: '" + diceStr + "'. Expected NdM or NdM+K.\r\n");
            return;
        }
        tapestry.world.setProperty(target.id, 'damage_dice', diceStr);
        admin.send(target.name + "'s damage dice set to " + diceStr + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'gold',
    help: 'set player gold [target] [amount] - set gold amount directly',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player gold [target] [amount]\r\n'); return; }
        var amount = parseInt(args[0], 10);
        if (isNaN(amount) || amount < 0) { admin.send('Gold cannot be negative.\r\n'); return; }
        tapestry.currency.setGold(target.id, amount, 'admin:set');
        var total = tapestry.currency.getGold(target.id);
        admin.send(target.name + "'s gold set to " + total + ".\r\n");
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'notell',
    help: 'set player notell [target] on|off - mute or restore tells for a player',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player notell [target] on|off\r\n'); return; }
        var flag = args[0].toLowerCase();
        if (flag !== 'on' && flag !== 'off') { admin.send('Expected: on or off\r\n'); return; }
        var value = flag === 'on';
        tapestry.world.setProperty(target.id, 'notell', value);
        if (value) {
            tapestry.world.send(target.id, 'You have been silenced from tells.\r\n');
            admin.send('Notell set on ' + target.name + '.\r\n');
        } else {
            tapestry.world.send(target.id, 'Your tells have been restored.\r\n');
            admin.send('Notell cleared on ' + target.name + '.\r\n');
        }
    }
});

tapestry.admin.set.register({
    kind: 'player',
    type: 'nochannels',
    help: 'set player nochannels [target] on|off - mute or restore all channels for a player',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player nochannels [target] on|off\r\n'); return; }
        var flag = args[0].toLowerCase();
        if (flag !== 'on' && flag !== 'off') { admin.send('Expected: on or off\r\n'); return; }
        var value = flag === 'on';
        tapestry.world.setProperty(target.id, 'nochannels', value);
        if (value) {
            tapestry.world.send(target.id, 'You have been silenced from all channels.\r\n');
            admin.send('Nochannels set on ' + target.name + '.\r\n');
        } else {
            tapestry.world.send(target.id, 'Your channel access has been restored.\r\n');
            admin.send('Nochannels cleared on ' + target.name + '.\r\n');
        }
    }
});
