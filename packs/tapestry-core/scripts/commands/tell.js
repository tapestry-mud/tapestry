tapestry.commands.register({
    name: 'tell',
    aliases: ['t'],
    description: 'Send a private message to a player.',
    category: 'social',
    roles: ['player'],
    args: {
        target: { type: 'player', required: true },
        message: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        var target = resolved.target;
        var message = resolved.message;

        if (tapestry.world.getProperty(actor.entityId, 'notell')) {
            actor.send('You cannot send tells right now.\r\n');
            return;
        }

        if (tapestry.world.getProperty(actor.entityId, 'nochannels')) {
            actor.send('You cannot use channels right now.\r\n');
            return;
        }

        if (tapestry.world.getProperty(target.id, 'notell')) {
            actor.send(target.name + ' is not accepting tells right now.\r\n');
            return;
        }

        actor.send('<tell>You tell ' + target.name + ': "' + message + '"</tell>\r\n');
        tapestry.world.send(target.id, '<tell>' + actor.name + ' tells you: "' + message + '"</tell>\r\n');
        tapestry.gmcp.send(target.id, 'Comm.Channel', { channel: 'tell', sender: actor.name, text: message });

        tapestry.world.setProperty(target.id, 'lastTellFrom', actor.entityId);
        tapestry.world.setProperty(actor.entityId, 'lastTellTo', target.id);
    }
});
