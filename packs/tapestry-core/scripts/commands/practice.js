function tierLabel(capValue) {
    if (capValue <= 25) { return 'Novice (cap 25%)'; }
    if (capValue <= 50) { return 'Apprentice (cap 50%)'; }
    if (capValue <= 75) { return 'Journeyman (cap 75%)'; }
    return 'Master (cap 100%)';
}

function renderPracticeList(player) {
    var learned = tapestry.abilities.getLearnedAbilities(player.entityId);
    if (learned.length === 0) {
        player.send('You have no learned abilities.\r\n');
        return;
    }

    var rows = [];
    for (var i = 0; i < learned.length; i++) {
        var a = learned[i];
        var cap = tapestry.training.getCap(player.entityId, a.id);
        var capNum = cap === 'novice' ? 25 : cap === 'apprentice' ? 50 : cap === 'journeyman' ? 75 : 100;
        var def = tapestry.abilities.getDefinition(a.id);
        var displayName = (def && def.short_name) ? def.short_name : a.id;
        rows.push({
            type: 'cell',
            cells: [
                { content: '  ' + displayName, width: 22 },
                { content: a.proficiency + '%', width: 8 },
                { content: tierLabel(capNum), width: 'fill' }
            ]
        });
    }

    var footerContent = 'Seek out a trainer to unlock higher proficiency.';
    var trainerResult = tapestry.training.findTrainerInRoom
        ? tapestry.training.findTrainerInRoom(player.entityId)
        : null;
    if (trainerResult) {
        footerContent = trainerResult.name + ' is here.  practice <ability> to train with them.';
    }

    var sections = [
        { rows: [{ type: 'title', left: 'Your Proficiencies', right: '' }] },
        { separatorAbove: 'minor', rows: rows },
        { separatorAbove: 'major', rows: [{ type: 'footer', content: footerContent }] }
    ];

    player.send('\r\n' + tapestry.ui.panel({ sections: sections }) + '\r\n');
}

tapestry.commands.register({
    name: 'practice',
    aliases: ['prac'],
    description: 'Show your proficiencies or practice with a teacher.',
    handler: function(player, args) {
        if (args.length === 0) {
            var learned = tapestry.abilities.getLearnedAbilities(player.entityId);
            var trainerResult = tapestry.training.findTrainerInRoom(player.entityId);
            var trainerName = trainerResult ? trainerResult.name : null;
            var trainerTier = trainerResult ? trainerResult.tier : null;
            var nextTierMap = {
                novice: 'apprentice',
                apprentice: 'journeyman',
                journeyman: 'master',
                master: null
            };

            tapestry.gmcp.send(player.entityId, 'Response.Training.Practice', {
                status: 'ok',
                trainer: trainerName,
                trainerTier: trainerTier,
                abilities: (learned || []).map(function(a) {
                    var capTier = tapestry.training.getCap(player.entityId, a.id);
                    var capNum = capTier === 'novice' ? 25
                        : capTier === 'apprentice' ? 50
                        : capTier === 'journeyman' ? 75 : 100;
                    var def = tapestry.abilities.getDefinition(a.id);
                    var displayName = (def && def.short_name) ? def.short_name
                        : (def ? def.name : a.id);
                    return {
                        id: a.id,
                        name: displayName,
                        proficiency: a.proficiency,
                        cap: capNum,
                        nextTier: nextTierMap[capTier] !== undefined ? nextTierMap[capTier] : null
                    };
                })
            });

            tapestry.respond.suppress(player.entityId);
            renderPracticeList(player);
            return;
        }

        var input = String(args[0]).toLowerCase();
        // Resolve keyword to full ability ID: match command_name, short ID (after last ':'), or exact ID
        var abilityId = input;
        var learned = tapestry.abilities.getLearnedAbilities(player.entityId);
        for (var i = 0; i < learned.length; i++) {
            var fullId = String(learned[i].id);
            var colonIdx = fullId.lastIndexOf(':');
            var shortId = colonIdx >= 0 ? fullId.substring(colonIdx + 1) : fullId;
            var def = tapestry.abilities.getDefinition(fullId);
            var cmdName = (def && def.command_name) ? String(def.command_name) : shortId;
            if (cmdName === input || shortId === input || fullId === input) {
                abilityId = fullId;
                break;
            }
        }
        var result = tapestry.training.practice(player.entityId, abilityId);
        if (result.kind === 'success') {
            var abilityDef = tapestry.abilities.getDefinition(abilityId);
            var displayName = (abilityDef && abilityDef.short_name) ? abilityDef.short_name : abilityId;
            var trainerResult = tapestry.training.findTrainerInRoom(player.entityId);
            var trainerName = trainerResult ? trainerResult.name : 'Your trainer';
            player.send(trainerName + ' teaches you more of ' + displayName + '.\r\n');
        } else {
            player.send(result.message + '\r\n');
        }
    }
});
