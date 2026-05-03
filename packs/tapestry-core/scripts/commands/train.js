var STAT_PREFIXES = {
    str: 'strength', int: 'intelligence', wis: 'wisdom',
    dex: 'dexterity', con: 'constitution', luc: 'luck', luck: 'luck'
};
var ALL_STATS = ['strength', 'intelligence', 'wisdom', 'dexterity', 'constitution', 'luck'];

function resolveStatName(input) {
    var lower = input.toLowerCase();
    if (STAT_PREFIXES[lower]) { return STAT_PREFIXES[lower]; }
    for (var i = 0; i < ALL_STATS.length; i++) {
        if (ALL_STATS[i].indexOf(lower) === 0) { return ALL_STATS[i]; }
    }
    return null;
}

function capitalize(s) { return s.charAt(0).toUpperCase() + s.slice(1); }

function renderTrainList(player) {
    var trains = tapestry.training.getTrainsAvailable(player.entityId);
    var raceId = tapestry.world.getProperty(player.entityId, 'race') || '';

    var rows = [];
    for (var i = 0; i < ALL_STATS.length; i++) {
        var s = ALL_STATS[i];
        var statObj = tapestry.stats.get(player.entityId);
        var current = statObj ? (statObj[s] || 0) : 0;
        var cap = tapestry.races.getStatCap ? tapestry.races.getStatCap(raceId, s) : 25;
        rows.push({
            type: 'cell',
            cells: [
                { content: '  ' + capitalize(s), width: 20 },
                { content: current + ' / ' + cap, width: 'fill' }
            ]
        });
    }

    var sections = [
        {
            rows: [{
                type: 'title',
                left: 'Your Attributes',
                right: 'Trains available:  ' + trains
            }]
        },
        { separatorAbove: 'minor', rows: rows },
        { separatorAbove: 'major', rows: [{ type: 'footer', content: 'train [stat] to spend a train.' }] }
    ];

    player.send('\r\n' + tapestry.ui.panel({ sections: sections }) + '\r\n');
}

tapestry.commands.register({
    name: 'train',
    description: 'Spend a train to raise a stat.',
    handler: function(player, args) {
        if (args.length === 0) {
            var trainsAvail = tapestry.training.getTrainsAvailable(player.entityId);
            var statsObj = tapestry.stats.get(player.entityId);

            tapestry.gmcp.send(player.entityId, 'Response.Training.Train', {
                status: 'ok',
                trainsRemaining: trainsAvail,
                stats: statsObj ? {
                    str: statsObj.strength,
                    int: statsObj.intelligence,
                    wis: statsObj.wisdom,
                    dex: statsObj.dexterity,
                    con: statsObj.constitution,
                    luk: statsObj.luck
                } : null
            });

            tapestry.respond.suppress(player.entityId);
            renderTrainList(player);
            return;
        }

        var statName = resolveStatName(args[0]);
        if (!statName) {
            player.send('That is not a valid stat. (str, int, wis, dex, con, luck)\r\n');
            return;
        }

        var result = tapestry.training.trainStat(player.entityId, statName);

        var statsAfter = tapestry.stats.get(player.entityId);
        tapestry.gmcp.send(player.entityId, 'Response.Training.Train', {
            status: result.kind === 'ok' ? 'ok' : 'error',
            message: result.message,
            trainsRemaining: tapestry.training.getTrainsAvailable(player.entityId),
            stats: result.kind === 'ok' && statsAfter ? {
                str: statsAfter.strength,
                int: statsAfter.intelligence,
                wis: statsAfter.wisdom,
                dex: statsAfter.dexterity,
                con: statsAfter.constitution,
                luk: statsAfter.luck
            } : undefined
        });

        tapestry.respond.suppress(player.entityId);
        player.send(result.message + '\r\n');
    }
});
