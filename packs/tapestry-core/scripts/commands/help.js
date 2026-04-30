tapestry.commands.register({
    name: 'help',
    aliases: ['?'],
    description: 'Show help for a command or topic.',
    priority: 0,
    handler: function(player, args) {
        var output = tapestry.ui.panel({
            sections: [{
                rows: [
                    { type: 'title', left: 'Tapestry Commands' },
                    { type: 'text', content: "  Movement:  north(n) south(s) east(e) west(w) up(u) down(d)" },
                    { type: 'text', content: '  Looking:   look(l) exits examine(ex)' },
                    { type: 'text', content: '  Items:     get(take) drop give put' },
                    { type: 'text', content: '  Equipment: wear wield remove equipment(eq)' },
                    { type: 'text', content: "  Talking:   say(') yell emote(:)" },
                    { type: 'text', content: '  Info:      who help(?) score inventory(i) motd' },
                    { type: 'text', content: '  Other:     recall quit(qq)' }
                ]
            }]
        });
        player.send('\r\n' + output + '\r\n');
    }
});
