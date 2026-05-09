function tierLabel(capValue) {
    if (capValue <= 25) { return 'Novice (cap 25%)'; }
    if (capValue <= 50) { return 'Apprentice (cap 50%)'; }
    if (capValue <= 75) { return 'Journeyman (cap 75%)'; }
    return 'Master (cap 100%)';
}

function renderPracticeList(actor) {
    var learned = tapestry.abilities.getLearnedAbilities(actor.entityId);
    if (learned.length === 0) {
        actor.send('You have no learned abilities.\r\n');
        return;
    }

    var rows = [];
    for (var i = 0; i < learned.length; i++) {
        var a = learned[i];
        var cap = tapestry.training.getCap(actor.entityId, a.id);
        var capNum = cap === 'novice' ? 25 : cap === 'apprentice' ? 50 : cap === 'journeyman' ? 75 : 100;
        var displayName = tapestry.abilities.getDisplayName(a.id);
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
        ? tapestry.training.findTrainerInRoom(actor.entityId)
        : null;
    if (trainerResult) {
        footerContent = trainerResult.name + ' is here.  practice [ability] to train with them.';
    }

    var sections = [
        { rows: [{ type: 'title', left: 'Your Proficiencies', right: '' }] },
        { separatorAbove: 'minor', rows: rows },
        { separatorAbove: 'major', rows: [{ type: 'footer', content: footerContent }] }
    ];

    actor.send('\r\n' + tapestry.ui.panel({ sections: sections }) + '\r\n');
}

tapestry.commands.register({
    name: 'practice',
    aliases: ['prac'],
    description: 'Show your proficiencies or practice with a trainer.',
    category: 'progression',
    roles: ['player'],
    args: {
        skill: { type: 'keyword', required: false }
    },
    handler: function(actor, resolved) {
        var skillInput = resolved.skill ? String(resolved.skill).toLowerCase() : null;

        if (!skillInput) {
            var learned = tapestry.abilities.getLearnedAbilities(actor.entityId);
            var trainerResult = tapestry.training.findTrainerInRoom(actor.entityId);
            var trainerName = trainerResult ? trainerResult.name : null;
            var trainerTier = trainerResult ? trainerResult.tier : null;
            var nextTierMap = {
                novice: 'apprentice',
                apprentice: 'journeyman',
                journeyman: 'master',
                master: null
            };

            tapestry.gmcp.send(actor.entityId, 'Response.Training.Practice', {
                status: 'ok',
                trainer: trainerName,
                trainerTier: trainerTier,
                abilities: (learned || []).map(function(a) {
                    var capTier = tapestry.training.getCap(actor.entityId, a.id);
                    var capNum = capTier === 'novice' ? 25
                        : capTier === 'apprentice' ? 50
                        : capTier === 'journeyman' ? 75 : 100;
                    var displayName = tapestry.abilities.getDisplayName(a.id);
                    return {
                        id: a.id,
                        name: displayName,
                        proficiency: a.proficiency,
                        cap: capNum,
                        nextTier: nextTierMap[capTier] !== undefined ? nextTierMap[capTier] : null
                    };
                })
            });

            tapestry.respond.suppress(actor.entityId);
            renderPracticeList(actor);
            return;
        }

        // Resolve keyword to full ability ID: match command_name, short ID (after last ':'), or exact ID
        var abilityId = skillInput;
        var learned = tapestry.abilities.getLearnedAbilities(actor.entityId);
        for (var i = 0; i < learned.length; i++) {
            var fullId = String(learned[i].id);
            var colonIdx = fullId.lastIndexOf(':');
            var shortId = colonIdx >= 0 ? fullId.substring(colonIdx + 1) : fullId;
            var def = tapestry.abilities.getDefinition(fullId);
            var cmdName = (def && def.command_name) ? String(def.command_name) : shortId;
            if (cmdName === skillInput || shortId === skillInput || fullId === skillInput) {
                abilityId = fullId;
                break;
            }
        }

        var result = tapestry.training.practice(actor.entityId, abilityId);
        if (result.kind === 'success') {
            var displayName = tapestry.abilities.getDisplayName(abilityId);
            var trainerResult = tapestry.training.findTrainerInRoom(actor.entityId);
            var trainerName = trainerResult ? trainerResult.name : 'Your trainer';
            actor.send(trainerName + ' teaches you more of ' + displayName + '.\r\n');
        } else {
            actor.send(result.message + '\r\n');
        }
    }
});
