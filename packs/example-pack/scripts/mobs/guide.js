tapestry.mobs.registerScript("example-pack:guide", {
    onSay: function(mob, player, text) {
        var lower = text.toLowerCase();

        if (/\b(help|hello|hi)\b/.test(lower)) {
            tapestry.mobs.command(mob.entityId,
                "say Hello, " + player.name + "! How can I help you today?");
            return;
        }

        if (lower.indexOf('blacksmith') >= 0 || lower.indexOf('equipment') >= 0 || lower.indexOf('weapon') >= 0) {
            tapestry.mobs.command(mob.entityId,
                "say The blacksmith is just south of here.");
            tapestry.mobs.command(mob.entityId,
                "emote points south.", 1.5);
            return;
        }

        if (lower.indexOf('inn') >= 0 || lower.indexOf('rest') >= 0 || lower.indexOf('sleep') >= 0) {
            tapestry.mobs.command(mob.entityId,
                "say The inn is to the north. Good rates, decent beds.");
            return;
        }
    }
});
