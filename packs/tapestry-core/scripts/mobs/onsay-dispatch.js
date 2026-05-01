tapestry.events.on("player.say", function(event) {
    var data = event.data;
    var mobs = tapestry.world.getEntitiesInRoom(data.roomId, "npc");

    for (var i = 0; i < mobs.length; i++) {
        var mob = mobs[i];
        var templateId = tapestry.world.getProperty(mob.id, "template_id");

        if (!templateId) {
            continue;
        }

        if (tapestry.combat.isInCombat(mob.id)) {
            continue;
        }

        tapestry.mobs.invokeHook(templateId, "onSay", {
            entityId: mob.id,
            name: mob.name,
            roomId: data.roomId
        }, {
            entityId: data.playerId,
            name: data.playerName
        }, data.text);
    }
});
