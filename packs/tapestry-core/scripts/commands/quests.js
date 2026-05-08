tapestry.commands.register({
    name: 'quests',
    description: 'List your active quests.',
    priority: 0,
    handler: function(player, args) {
        var state = tapestry.quests.getState(player.entityId);
        if (!state || state.active.length === 0) {
            player.send('You have no active quests.\r\n');
            return;
        }

        player.send('[ Active Quests ]\r\n');
        state.active.forEach(function(q, i) {
            var stageNum = q.stageIndex + 1;
            var stageCount = q.stageCount;
            var objectives = q.objectives
                .filter(function(o) { return !o.complete; })
                .map(function(o) { return o.description + ' [' + o.current + '/' + o.required + ']'; })
                .join(', ');
            player.send('  ' + (i + 1) + '. ' + q.name + '  (' + q.type + ')  Stage ' + stageNum + '/' + stageCount + ' -- ' + (objectives || 'complete') + '\r\n');
        });
    }
});

tapestry.commands.register({
    name: 'quest',
    description: 'Show quest detail or abandon a quest.',
    priority: 0,
    handler: function(player, args) {
        if (args.length === 0) {
            player.send('Usage: quest [name] | quest abandon [name]\r\n');
            return;
        }

        if (args[0].toLowerCase() === 'abandon') {
            var name = args.slice(1).join(' ').toLowerCase();
            var state = tapestry.quests.getState(player.entityId);
            if (!state) {
                player.send('You have no active quests.\r\n');
                return;
            }
            var match = state.active.find(function(q) {
                return q.name.toLowerCase().indexOf(name) !== -1;
            });
            if (!match) {
                player.send('No active quest matches that name.\r\n');
                return;
            }
            tapestry.quests.abandon(player.entityId, match.questId);
            player.send('You abandon "' + match.name + '".\r\n');
            return;
        }

        var search = args.join(' ').toLowerCase();
        var state = tapestry.quests.getState(player.entityId);
        if (!state) {
            player.send('You have no active quests.\r\n');
            return;
        }
        var match = state.active.find(function(q) {
            return q.name.toLowerCase().indexOf(search) !== -1;
        });
        if (!match) {
            player.send('No active quest matches "' + search + '".\r\n');
            return;
        }

        player.send('[ ' + match.name + ' ]  (' + match.type + ')\r\n');
        player.send('Stage ' + (match.stageIndex + 1) + ' of ' + match.stageCount + '\r\n');
        match.objectives.forEach(function(o) {
            var status = o.complete ? '[done]' : '[' + o.current + '/' + o.required + ']';
            player.send('  ' + status + ' ' + (o.description || o.objectiveId) + '\r\n');
        });
    }
});
