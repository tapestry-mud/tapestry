function adminSetResolveTarget(kind, name) {
    var lower = name.toLowerCase();
    if (kind === 'player') {
        var players = tapestry.world.getOnlinePlayers();
        for (var i = 0; i < players.length; i++) {
            if (players[i].name.toLowerCase() === lower) {
                return { id: players[i].id, name: players[i].name };
            }
        }
        return null;
    }
    if (kind === 'npc') {
        var npcs = tapestry.world.getNpcsInWorld();
        for (var n = 0; n < npcs.length; n++) {
            if (npcs[n].name.toLowerCase().indexOf(lower) !== -1) {
                return { id: npcs[n].id, name: npcs[n].name };
            }
        }
        return null;
    }
    if (kind === 'item') {
        var items = tapestry.world.getItemsInWorld();
        for (var k = 0; k < items.length; k++) {
            if (items[k].name.toLowerCase().indexOf(lower) !== -1) {
                return { id: items[k].id, name: items[k].name };
            }
        }
        return null;
    }
    return null;
}

var adminSetHandlers = {
    'player:alignment': {
        usage: 'set player alignment [target] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.alignment.set(target.id, n, 'admin_set');
            var actual = tapestry.alignment.get(target.id);
            var bucket = tapestry.alignment.bucket(target.id);
            actor.send(target.name + "'s alignment set to " + actual + " (" + bucket + ").\r\n");
        }
    },
    'player:str': {
        usage: 'set player str [target] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.stats.setBase(target.id, 'strength', n);
            actor.send(target.name + "'s Strength set to " + n + ".\r\n");
        }
    },
    'player:int': {
        usage: 'set player int [target] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.stats.setBase(target.id, 'intelligence', n);
            actor.send(target.name + "'s Intelligence set to " + n + ".\r\n");
        }
    },
    'player:wis': {
        usage: 'set player wis [target] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.stats.setBase(target.id, 'wisdom', n);
            actor.send(target.name + "'s Wisdom set to " + n + ".\r\n");
        }
    },
    'player:dex': {
        usage: 'set player dex [target] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.stats.setBase(target.id, 'dexterity', n);
            actor.send(target.name + "'s Dexterity set to " + n + ".\r\n");
        }
    },
    'player:con': {
        usage: 'set player con [target] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.stats.setBase(target.id, 'constitution', n);
            actor.send(target.name + "'s Constitution set to " + n + ".\r\n");
        }
    },
    'player:luck': {
        usage: 'set player luck [target] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.stats.setBase(target.id, 'luck', n);
            actor.send(target.name + "'s Luck set to " + n + ".\r\n");
        }
    },
    'player:prof': {
        usage: 'set player prof [target] [ability] [value]',
        handler: function(actor, target, rest) {
            var parts = rest.split(' ');
            if (parts.length < 2) { actor.send('Usage: set player prof [target] [ability] [value]\r\n'); return; }
            var abilityId = parts[0];
            var n = parseInt(parts[1], 10);
            if (isNaN(n)) { actor.send('Value must be a number.\r\n'); return; }
            tapestry.abilities.setProficiency(target.id, abilityId, n);
            actor.send(target.name + "'s " + abilityId + " proficiency set to " + n + "%.\r\n");
        }
    },
    'player:cap': {
        usage: 'set player cap [target] [ability] [novice|apprentice|journeyman|master]',
        handler: function(actor, target, rest) {
            var parts = rest.split(' ');
            if (parts.length < 2) { actor.send('Usage: set player cap [target] [ability] [novice|apprentice|journeyman|master]\r\n'); return; }
            var abilityId = parts[0];
            var tier = parts[1].toLowerCase();
            if (!['novice', 'apprentice', 'journeyman', 'master'].includes(tier)) {
                actor.send('Invalid tier. Use: novice, apprentice, journeyman, master.\r\n');
                return;
            }
            tapestry.training.setCap(target.id, abilityId, tier);
            actor.send(target.name + "'s " + abilityId + " cap set to " + tier + ".\r\n");
        }
    },
    'player:gold': {
        usage: 'set player gold [target] [amount]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n) || n < 0) { actor.send('Gold cannot be negative.\r\n'); return; }
            tapestry.currency.setGold(target.id, n, 'admin:set');
            var total = tapestry.currency.getGold(target.id);
            actor.send(target.name + "'s gold set to " + total + ".\r\n");
        }
    },
    'player:notell': {
        usage: 'set player notell [target] on|off',
        handler: function(actor, target, rest) {
            var flag = rest.toLowerCase();
            if (flag !== 'on' && flag !== 'off') { actor.send('Expected: on or off\r\n'); return; }
            var enabled = flag === 'on';
            tapestry.world.setProperty(target.id, 'notell', enabled);
            if (enabled) {
                tapestry.world.send(target.id, 'You have been silenced from tells.\r\n');
                actor.send('Notell set on ' + target.name + '.\r\n');
            } else {
                tapestry.world.send(target.id, 'Your tells have been restored.\r\n');
                actor.send('Notell cleared on ' + target.name + '.\r\n');
            }
        }
    },
    'player:nochannels': {
        usage: 'set player nochannels [target] on|off',
        handler: function(actor, target, rest) {
            var flag = rest.toLowerCase();
            if (flag !== 'on' && flag !== 'off') { actor.send('Expected: on or off\r\n'); return; }
            var enabled = flag === 'on';
            tapestry.world.setProperty(target.id, 'nochannels', enabled);
            if (enabled) {
                tapestry.world.send(target.id, 'You have been silenced from all channels.\r\n');
                actor.send('Nochannels set on ' + target.name + '.\r\n');
            } else {
                tapestry.world.send(target.id, 'Your channel access has been restored.\r\n');
                actor.send('Nochannels cleared on ' + target.name + '.\r\n');
            }
        }
    },
    'npc:hp': {
        usage: 'set npc hp [mob] [value]',
        handler: function(actor, target, rest) {
            var n = parseInt(rest, 10);
            if (isNaN(n) || n < 0) { actor.send('Value must be a non-negative number.\r\n'); return; }
            tapestry.admin.setEntityHp(target.id, n);
            actor.send(target.name + "'s hp set to " + n + " (hp and max hp).\r\n");
        }
    },
    'item:dice': {
        usage: 'set item dice [item] [dice-string]',
        handler: function(actor, target, rest) {
            if (!/^\d+d\d+([+-]\d+)?$/.test(rest)) {
                actor.send("Invalid dice string: '" + rest + "'. Expected NdM or NdM+K.\r\n");
                return;
            }
            tapestry.world.setProperty(target.id, 'damage_dice', rest);
            actor.send(target.name + "'s damage dice set to " + rest + ".\r\n");
        }
    }
};

tapestry.commands.register({
    name: 'set',
    description: 'Admin: modify player/npc/item fields.',
    category: 'admin',
    admin: true,
    args: {
        entity: { type: 'keyword', required: true },
        property: { type: 'keyword', required: true },
        value: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        var entityKind = resolved.entity.toLowerCase();
        var property = resolved.property.toLowerCase();
        var value = resolved.value;
        var key = entityKind + ':' + property;
        var entry = adminSetHandlers[key];

        if (!entry) {
            actor.send('Unknown set target: ' + key + '\r\n');
            return;
        }

        // value is "targetName rest..." -- split on first space
        var spaceIdx = value.indexOf(' ');
        var targetName;
        var rest;
        if (spaceIdx === -1) {
            targetName = value;
            rest = '';
        } else {
            targetName = value.slice(0, spaceIdx);
            rest = value.slice(spaceIdx + 1);
        }

        var target = adminSetResolveTarget(entityKind, targetName);
        if (!target) {
            actor.send("No " + entityKind + " named '" + targetName + "' found.\r\n");
            return;
        }

        entry.handler(actor, target, rest);
    }
});
