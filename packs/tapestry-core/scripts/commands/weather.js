tapestry.commands.register({
    name: 'weather',
    description: 'Show the current weather.',
    category: 'info',
    roles: ['player'],
    args: {},
    priority: 0,
    handler: function(actor, resolved) {
        var roomId = actor.roomId;
        if (!roomId) { actor.send("You are nowhere.\r\n"); return; }
        var areaId = tapestry.world.getRoomArea(roomId);
        if (!areaId) { actor.send("This area has no weather.\r\n"); return; }
        var state = tapestry.weather.current(areaId);
        actor.send("The weather here: " + state + ".\r\n");
    }
});
