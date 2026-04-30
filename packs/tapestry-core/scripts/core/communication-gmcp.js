tapestry.events.on("communication.message", function(event) {
    var data = event.data;
    var recipients = [];

    if (data.channel === "say" || data.channel === "emote") {
        recipients = tapestry.world.getEntitiesInRoom(data.roomId, "player");
    } else if (data.channel === "tell" || data.channel === "whisper") {
        if (data.targetId) {
            recipients = [{ id: data.targetId }];
        }
    } else {
        recipients = tapestry.world.getOnlinePlayers();
    }

    var payload = {
        channel: data.channel,
        sender: data.sender,
        senderId: data.senderId,
        source: data.source,
        text: data.text
    };

    for (var i = 0; i < recipients.length; i++) {
        if (data.source === "player" && recipients[i].id === data.senderId) {
            continue;
        }
        tapestry.gmcp.send(recipients[i].id, "Comm.Channel", payload);
    }
});
