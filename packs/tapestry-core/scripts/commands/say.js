tapestry.commands.register({
    name: 'say',
    aliases: ["'"],
    description: 'Speak to others in the room.',
    category: 'communication',
    roles: ['player', 'mob'],
    gmcp: { channel: 'say', prependSender: false },
    args: {
        message: { type: 'text', required: true }
    },
    handler: function(actor, resolved) {
        actor.send('You say "<highlight>' + resolved.message + '</highlight>"\r\n');
        tapestry.world.sendToRoomExcept(
            actor.roomId,
            actor.entityId,
            actor.name + ' says "<highlight>' + resolved.message + '</highlight>"\r\n'
        );

        tapestry.events.publish("communication.message", {
            channel: "say",
            sender: actor.name,
            senderId: actor.entityId,
            source: "player",
            text: resolved.message,
            roomId: actor.roomId
        });

        tapestry.events.publish("player.say", {
            playerId: actor.entityId,
            playerName: actor.name,
            roomId: actor.roomId,
            text: resolved.message
        });
    }
});
