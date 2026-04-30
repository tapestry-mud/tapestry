tapestry.commands.register({
    name: 'immtalk',
    aliases: [';'],
    description: 'Send a message on the immortal channel',
    category: 'communication',
    admin: true,
    handler: function(player, args) {
        if (!player.hasTag('admin')) {
            player.send('Huh?\r\n');
            return;
        }

        var message = args.join(' ');
        if (!message) {
            player.send('Immtalk what?\r\n');
            return;
        }

        if (tapestry.world.getProperty(player.entityId, 'nochannels')) {
            player.send('You cannot use channels right now.\r\n');
            return;
        }

        var admins = tapestry.world.getOnlinePlayers();
        for (var i = 0; i < admins.length; i++) {
            var tags = tapestry.world.getEntityTags(admins[i].id);
            if (tags.indexOf('admin') !== -1) {
                tapestry.world.send(admins[i].id, '<imm>[Imm] ' + player.name + ': "' + message + '"</imm>\r\n');
                tapestry.gmcp.send(admins[i].id, 'Comm.Channel', { channel: 'imm', sender: player.name, text: message });
            }
        }
    }
});
