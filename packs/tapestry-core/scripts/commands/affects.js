tapestry.commands.register({
    name: 'affects',
    aliases: ['aff'],
    description: 'Show active effects on your character',
    handler: function(player, args) {
        var active = tapestry.effects.getActive(player.entityId);

        if (!active || active.length === 0) {
            var output = tapestry.ui.panel({
                sections: [{
                    rows: [
                        { type: 'title', left: 'Active Effects' },
                        { type: 'text', content: '  You are not affected by anything.' }
                    ]
                }]
            });
            player.send('\r\n' + output + '\r\n');
            return;
        }

        var rows = [{ type: 'title', left: 'Active Effects' }];

        for (var i = 0; i < active.length; i++) {
            var effect = active[i];
            var name = effect.name || effect.id;
            var isHarmful = false;

            if (effect.flags) {
                for (var f = 0; f < effect.flags.length; f++) {
                    if (effect.flags[f] === 'harmful') {
                        isHarmful = true;
                        break;
                    }
                }
            }

            var durationText;
            if (effect.remaining_pulses < 0) {
                durationText = 'permanent';
            } else {
                durationText = effect.remaining_pulses + ' pulses';
            }

            var typeTag = isHarmful ? 'debuff' : 'buff';
            var line = '  <' + typeTag + '>' + name + '</' + typeTag + '>';
            rows.push({
                type: 'cell',
                cells: [
                    { content: line, width: 40 },
                    { content: durationText, width: 'fill', align: 'right' }
                ]
            });
        }

        var output = tapestry.ui.panel({ sections: [{ rows: rows }] });
        player.send('\r\n' + output + '\r\n');
    }
});
