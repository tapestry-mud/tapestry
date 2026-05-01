tapestry.mobs.registerCommand("say", {
    gmcp: { channel: "say" },
    handler: function(mob, text) {
        if (tapestry.world.getEntitiesInRoom(mob.roomId, "player").length < 1) {
            return;
        }
        tapestry.world.sendToRoom(mob.roomId,
            mob.name + ' says "<highlight>' + text + '</highlight>"\r\n');
    }
});

tapestry.mobs.registerCommand("emote", {
    gmcp: { channel: "emote", prependSender: true },
    handler: function(mob, text) {
        if (tapestry.world.getEntitiesInRoom(mob.roomId, "player").length < 1) {
            return;
        }
        tapestry.world.sendToRoom(mob.roomId, mob.name + ' ' + text + '\r\n');
    }
});
