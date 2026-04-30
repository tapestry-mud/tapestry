tapestry.commands.register({
    name: 'weather',
    description: 'Show the current weather.',
    priority: 0,
    handler: function(player, args) {
        var roomId = player.roomId;
        if (!roomId) { player.send("You are nowhere.\r\n"); return; }
        var areaId = tapestry.world.getRoomArea(roomId);
        if (!areaId) { player.send("This area has no weather.\r\n"); return; }
        var state = tapestry.weather.current(areaId);
        player.send("The weather here: " + state + ".\r\n");
    }
});
