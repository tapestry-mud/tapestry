// Loot protection for player corpses
// Pack config property: player_corpse_loot_policy
//   "owner_only" — only the corpse owner can loot
//   "permission" — owner can grant access (future: allow command)
//   "none" — anyone can loot (PvP loot enabled)
//
// No tapestry.config module exists yet — defaulting to "owner_only"

tapestry.events.on("container.access.check", function(event) {
    var container = tapestry.world.getEntity(event.targetEntityId);
    if (!container) {
        return;
    }

    // Only enforce policy on player corpses
    if (!container.tags || container.tags.indexOf("player_corpse") < 0) {
        return;
    }

    // Default policy: owner_only
    // When tapestry.config is available, replace with:
    //   var policy = tapestry.config.get("player_corpse_loot_policy") || "owner_only";
    var policy = "owner_only";
    var owner = container.properties && container.properties.owner;

    if (policy === "none") {
        // Anyone can loot — no restriction
        return;
    }

    if (policy === "owner_only" || policy === "permission") {
        // Only owner can access
        if (event.sourceEntityId !== owner) {
            event.cancel();
            var accessor = tapestry.world.getEntity(event.sourceEntityId);
            if (accessor) {
                tapestry.world.send(event.sourceEntityId, "That corpse doesn't belong to you.\r\n");
            }
        }
    }
});
