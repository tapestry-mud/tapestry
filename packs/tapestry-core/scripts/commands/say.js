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

        var inRoom = tapestry.world.getEntitiesInRoom(player.roomId, 'player');
        for (var i = 0; i < inRoom.length; i++) {
            if (inRoom[i].id !== player.entityId) {
                tapestry.gmcp.send(inRoom[i].id, 'Comm.Channel', { channel: 'say', sender: player.name, text: message });
            }
        }
    }
});
