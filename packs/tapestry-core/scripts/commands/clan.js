tapestry.commands.register({
    name: 'clan',
    description: 'Send a message to your clan',
    category: 'communication',
    handler: function(player, args) {
        var message = args.join(' ');
        if (!message) {
            player.send('Clan what?\r\n');
            return;
        }

        var allTags = tapestry.world.getEntityTags(player.entityId);
        var clanTag = null;
        for (var i = 0; i < allTags.length; i++) {
            if (allTags[i].indexOf('clan:') === 0) {
                clanTag = allTags[i];
                break;
            }
        }

        if (!clanTag) {
            player.send('You are not in a clan.\r\n');
            return;
        }

        if (tapestry.world.getProperty(player.entityId, 'nochannels')) {
            player.send('You cannot use channels right now.\r\n');
            return;
        }

        var online = tapestry.world.getOnlinePlayers();
        for (var j = 0; j < online.length; j++) {
            var memberTags = tapestry.world.getEntityTags(online[j].id);
            if (memberTags.indexOf(clanTag) !== -1) {
                tapestry.world.send(online[j].id, '<clan>[Clan] ' + player.name + ': "' + message + '"</clan>\r\n');
                tapestry.gmcp.send(online[j].id, 'Comm.Channel', { channel: 'clan', sender: player.name, text: message });
            }
        }
    }
});
