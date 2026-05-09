tapestry.commands.register({
    name: 'time',
    description: 'Show the current in-game time.',
    category: 'info',
    roles: ['player'],
    args: {},
    priority: 0,
    handler: function(actor, resolved) {
        var h = tapestry.time.hour();
        var p = tapestry.time.period();
        var labels = ["midnight","early morning","early morning","predawn","predawn",
                      "dawn","morning","morning","mid-morning","mid-morning",
                      "late morning","late morning","noon","afternoon","afternoon",
                      "mid-afternoon","mid-afternoon","late afternoon","late afternoon",
                      "dusk","evening","evening","night","night"];
        var label = labels[h] || p;
        actor.send("It is " + label + " (hour " + h + "). Period: " + p + ".\r\n");
    }
});
