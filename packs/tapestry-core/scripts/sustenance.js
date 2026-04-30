tapestry.admin.set.register({
    kind: 'player',
    type: 'sustenance',
    help: 'set player sustenance [target] [value] - set sustenance (0-100)',
    handler: function(admin, target, args) {
        if (args.length < 1) { admin.send('Usage: set player sustenance [target] [value]\r\n'); return; }
        var value = parseInt(args[0], 10);
        if (isNaN(value) || value < 0 || value > 100) { admin.send('Value must be 0-100.\r\n'); return; }
        tapestry.world.setProperty(target.id, 'sustenance', value);
        admin.send(target.name + "'s sustenance set to " + value + ".\r\n");
    }
});

tapestry.events.on('sustenance.changed', function(evt) {
    var entityId = evt.data.entityId;
    var newTier = evt.data.newTier;
    if (newTier === 'hungry') {
        tapestry.world.send(entityId, 'You are getting hungry.\r\n');
    } else if (newTier === 'famished') {
        tapestry.world.send(entityId, 'You are famished! Your body aches with hunger.\r\n');
    } else if (newTier === 'full') {
        tapestry.world.send(entityId, 'You feel satisfied.\r\n');
    }
});

tapestry.events.on('sustenance.reminder', function(evt) {
    var entityId = evt.data.entityId;
    var tier = evt.data.tier;
    if (tier === 'hungry') {
        tapestry.world.send(entityId, 'You are hungry.\r\n');
    } else if (tier === 'famished') {
        tapestry.world.send(entityId, 'You are famished and can barely think straight.\r\n');
    }
});

tapestry.events.on('item.consumed', function(evt) {
    var effectId = evt.data.effectId;
    var effectData = evt.data.effectData;
    var entityId = evt.data.entityId;

    if (effectId === 'core:instant-heal' && effectData) {
        var healHp = effectData.heal_hp || 0;
        if (healHp > 0) {
            tapestry.stats.addVital(entityId, 'hp', healHp);
            tapestry.world.send(entityId,
                'You feel a warm surge of healing energy. (+' + healHp + ' HP)\r\n');
        }
    }

    if (effectId === 'core:instant-restore' && effectData) {
        var healResource = effectData.heal_resource || 0;
        if (healResource > 0) {
            tapestry.stats.addVital(entityId, 'resource', healResource);
            tapestry.world.send(entityId,
                'You feel your power restored. (+' + healResource + ')\r\n');
        }
    }
});
