// tapestry-core pack initialization
// Runs first -- registers rarity tiers, essences, and display config before commands load.

tapestry.rarity.register({ key: 'common',   order: 0, displayText: null,       decorators: null,                          color: 'white',   html: 'text-ansi-white',          visible: false });
tapestry.rarity.register({ key: 'uncommon', order: 1, displayText: 'Uncommon', decorators: { left: '-= ', right: ' =-' }, color: 'white',   html: 'text-ansi-white',          visible: true  });
tapestry.rarity.register({ key: 'rare',     order: 2, displayText: 'Rare',     decorators: { left: '-= ', right: ' =-' }, color: 'green',   html: 'text-ansi-bright-green',   visible: true  });
tapestry.rarity.register({ key: 'epic',     order: 3, displayText: 'Epic',     decorators: { left: '-= ', right: ' =-' }, color: 'cyan',    html: 'text-ansi-bright-cyan',    visible: true  });
tapestry.rarity.register({ key: 'artifact', order: 4, displayText: 'Artifact', decorators: { left: '-==', right: '==-' }, color: 'yellow',  html: 'text-ansi-bright-yellow',  visible: true  });
tapestry.rarity.register({ key: 'ooak',     order: 5, displayText: 'One of a Kind', decorators: { left: '-==', right: '==-' }, color: 'yellow',  html: 'text-ansi-bright-magenta', visible: true  });

tapestry.essence.register({ key: 'fire',   glyph: '^', color: 'red',     html: 'text-ansi-bright-red'     });
tapestry.essence.register({ key: 'shadow', glyph: '~', color: 'magenta', html: 'text-ansi-bright-magenta' });
tapestry.essence.register({ key: 'storm',  glyph: '*', color: 'cyan',    html: 'text-ansi-bright-cyan'    });
tapestry.essence.register({ key: 'earth',  glyph: '#', color: 'yellow',  html: 'text-ansi-bright-yellow'  });

tapestry.equipment.setEmptyText('-nothing-');
