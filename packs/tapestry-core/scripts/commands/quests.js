tapestry.commands.register({
    name: 'quests',
    description: 'List your active quests.',
    category: 'progression',
    roles: ['player'],
    args: {},
    handler: function(actor, resolved) {
        var state = tapestry.quests.getState(actor.entityId);
        if (!state || state.active.length === 0) {
            actor.send('You have no active quests.\r\n');
            return;
        }

        actor.send('[ Active Quests ]\r\n');
        state.active.forEach(function(q, i) {
            var stageNum = q.stageIndex + 1;
            var stageCount = q.stageCount;
            var objectives = q.objectives
                .filter(function(o) { return !o.complete; })
                .map(function(o) { return o.description + ' [' + o.current + '/' + o.required + ']'; })
                .join(', ');
            actor.send('  ' + (i + 1) + '. ' + q.name + '  (' + q.type + ')  Stage ' + stageNum + '/' + stageCount + ' - ' + (objectives || 'complete') + '\r\n');
        });
    }
});

tapestry.commands.register({
    name: 'quest',
    description: 'Show quest detail or abandon a quest.',
    category: 'progression',
    roles: ['player'],
    args: {
        subcommand: { type: 'keyword', required: false },
        questName: { type: 'keyword', required: false }
    },
    handler: function(actor, resolved) {
        var subcommand = resolved.subcommand;
        var questName = resolved.questName;

        if (!subcommand) {
            actor.send('Usage: quest [name] | quest abandon [name]\r\n');
            return;
        }

        if (subcommand.toLowerCase() === 'abandon') {
            if (!questName) {
                actor.send('Abandon which quest?\r\n');
                return;
            }
            var state = tapestry.quests.getState(actor.entityId);
            if (!state) {
                actor.send('You have no active quests.\r\n');
                return;
            }
            var match = state.active.find(function(q) {
                return q.name.toLowerCase().indexOf(questName.toLowerCase()) !== -1;
            });
            if (!match) {
                actor.send('No active quest matches that name.\r\n');
                return;
            }
            tapestry.quests.abandon(actor.entityId, match.questId);
            actor.send('You abandon "' + match.name + '".\r\n');
            return;
        }

        // subcommand is the search term; questName may extend it
        var search = questName
            ? subcommand.toLowerCase() + ' ' + questName.toLowerCase()
            : subcommand.toLowerCase();
        var questState = tapestry.quests.getState(actor.entityId);
        if (!questState) {
            actor.send('You have no active quests.\r\n');
            return;
        }
        var detail = questState.active.find(function(q) {
            return q.name.toLowerCase().indexOf(search) !== -1;
        });
        if (!detail) {
            actor.send('No active quest matches "' + search + '".\r\n');
            return;
        }

        actor.send('[ ' + detail.name + ' ]  (' + detail.type + ')\r\n');
        actor.send('Stage ' + (detail.stageIndex + 1) + ' of ' + detail.stageCount + '\r\n');
        detail.objectives.forEach(function(o) {
            var status = o.complete ? '[done]' : '[' + o.current + '/' + o.required + ']';
            actor.send('  ' + status + ' ' + (o.description || o.objectiveId) + '\r\n');
        });
    }
});
