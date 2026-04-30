// Handle NPC death — create corpse container with mob's items
tapestry.events.on("entity.vital.depleted", function(event) {
    // Only handle NPCs
    var entity = tapestry.world.getEntity(event.sourceEntityId);
    if (!entity || entity.type !== "npc") {
        return;
    }

    // Skip no-kill entities (safety check)
    if (entity.tags && entity.tags.indexOf("no-kill") >= 0) {
        return;
    }

    var roomId = entity.roomId;
    var mobName = entity.name;
    var templateId = entity.properties ? entity.properties.template_id : null;
    var corpseDecay = (entity.properties ? entity.properties.corpse_decay : null) || 300;

    // Create corpse container
    var corpseId = tapestry.world.createEntity("container", "the corpse of " + mobName);
    tapestry.world.addTag(corpseId, "corpse");
    tapestry.world.addTag(corpseId, "container");
    tapestry.world.setProperty(corpseId, "corpse_decay", corpseDecay);
    tapestry.world.setProperty(corpseId, "corpse_created_tick", tapestry.world.getCurrentTick());
    tapestry.world.setProperty(corpseId, "template_id", templateId);

    // Transfer equipment and inventory to corpse
    tapestry.inventory.transferAll(event.sourceEntityId, corpseId);
    tapestry.equipment.transferAll(event.sourceEntityId, corpseId);

    // Place corpse in room
    tapestry.world.placeEntity(corpseId, roomId);

    // Remove mob from world
    tapestry.world.removeEntity(event.sourceEntityId);

    // Notify room
    tapestry.world.sendToRoom(roomId, mobName + " has died!\r\n");

    // Publish death event for other systems
    tapestry.events.publish("mob.death", {
        templateId: templateId,
        roomId: roomId,
        corpseId: corpseId,
        killerId: event.data ? event.data.killerId : null
    });
});
