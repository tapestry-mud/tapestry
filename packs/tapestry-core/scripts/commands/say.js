tapestry.commands.register({
    name: 'say',
    aliases: ["'"],
    description: 'Speak to others in the room.',
    priority: 0,
    handler: function(player, args) {
        var message = args.join(' ');
        if (!message) {
            player.send('Say what?\r\n');
            return;
        }
        player.send('You say "<highlight>' + message + '</highlight>"\r\n');
        tapestry.world.sendToRoomExcept(
            player.roomId,
            player.entityId,
            player.name + ' says "<highlight>' + message + '</highlight>"\r\n'
        );

        tapestry.events.publish("communication.message", {
            channel: "say",
            sender: player.name,
            senderId: player.entityId,
            source: "player",
            text: message,
            roomId: player.roomId
        });

        tapestry.events.publish("player.say", {
            playerId: player.entityId,
            playerName: player.name,
            roomId: player.roomId,
            text: message
        });
    }
});
