tapestry.commands.register({
    name: 'clan',
    description: 'Send a message to your clan.',
    category: 'social',
    roles: ['player'],
    args: {
        message: { type: 'rest', required: true, prompt: 'Clan what?' }
    },
    handler: function(actor, resolved) {
        var message = resolved.message;

        var allTags = tapestry.world.getEntityTags(actor.entityId);
        var clanTag = null;
        for (var i = 0; i < allTags.length; i++) {
            if (allTags[i].indexOf('clan:') === 0) {
                clanTag = allTags[i];
                break;
            }
        }

        if (!clanTag) {
            actor.send('You are not in a clan.\r\n');
            return;
        }

        if (tapestry.world.getProperty(actor.entityId, 'nochannels')) {
            actor.send('You cannot use channels right now.\r\n');
            return;
        }

        var online = tapestry.world.getOnlinePlayers();
        for (var j = 0; j < online.length; j++) {
            var memberTags = tapestry.world.getEntityTags(online[j].id);
            if (memberTags.indexOf(clanTag) !== -1) {
                tapestry.world.send(online[j].id, '<clan>[Clan] ' + actor.name + ': "' + message + '"</clan>\r\n');
                tapestry.gmcp.send(online[j].id, 'Comm.Channel', { channel: 'clan', sender: actor.name, text: message });
            }
        }
    }
});
