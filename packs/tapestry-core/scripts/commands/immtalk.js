tapestry.commands.register({
    name: 'immtalk',
    aliases: [';'],
    description: 'Send a message on the immortal channel.',
    category: 'social',
    admin: true,
    args: {
        message: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        if (tapestry.world.getProperty(actor.entityId, 'nochannels')) {
            actor.send('You cannot use channels right now.\r\n');
            return;
        }

        var message = resolved.message;
        var admins = tapestry.world.getOnlinePlayers();

        for (var i = 0; i < admins.length; i++) {
            var target = admins[i];
            if (tapestry.world.hasRole(target.id, 'admin')) {
                tapestry.world.send(target.id, '<imm>[Imm] ' + actor.name + ': "' + message + '"</imm>\r\n');
                tapestry.gmcp.send(target.id, 'Comm.Channel', { channel: 'imm', sender: actor.name, text: message });
            }
        }
    }
});
