tapestry.commands.register({
    name: 'emote',
    aliases: [':'],
    description: 'Perform an emote action.',
    priority: 0,
    handler: function(player, args) {
        var action = args.join(' ');
        if (!action) {
            player.send('Emote what?\r\n');
            return;
        }
        player.send(player.name + ' ' + action + '\r\n');
        tapestry.world.sendToRoomExcept(
            player.roomId,
            player.entityId,
            player.name + ' ' + action + '\r\n'
        );

        tapestry.events.publish("communication.message", {
            channel: "emote",
            sender: player.name,
            senderId: player.entityId,
            source: "player",
            text: player.name + ' ' + action,
            roomId: player.roomId
        });
    }
});
