// packs/tapestry-core/scripts/decay-events.js
tapestry.events.on('corpse.decayed', function(evt) {
    var roomId = evt.data.roomId;
    var itemIds = evt.data.itemIds || [];

    if (roomId && itemIds.length > 0) {
        tapestry.world.sendToRoom(roomId,
            'Its belongings scatter to the ground.\r\n');
    }
});
