'use strict';

function manifestTemplate(scopedName) {
  return `# Package manifest -- fill in the TODOs before publishing.
name: "${scopedName}"
version: "0.1.0"
type: "module"        # core | module | world
display_name: "TODO: Human-readable name"
description: "TODO: One-line description for registry search"
author:
  name: "TODO: Your Name"
  handle: "TODO: your-registry-handle"
license: "MIT"

# Semver range: >=3.0.0 means any engine version at or above this.
engine: ">=3.0.0"

# ^ means compatible minor/patch changes (>=1.0.0 <2.0.0)
dependencies:
  "@tapestry/core": "^1.0.0"

# Optional: warn if not installed, never auto-installed
# peerDependencies:
#   "@tapestry/sustenance": "^1.0.0"

# Capabilities this pack provides (for reverse-dependency lookups)
provides:
  - example

# strict: undeclared tags on entities cause load failure
# lenient: undeclared tags log warnings, pack still loads
tag_validation: strict

# Path to tag declarations file
tags: "tags.yml"

# Glob patterns -- the engine uses these to find your content
content:
  areas: "areas/**/area.yaml"
  rooms: "areas/**/rooms/*.yaml"
  items: "areas/**/items/*.yaml"
  mobs: "areas/**/mobs/*.yaml"
  scripts: "scripts/**/*.js"
  help: "help/**/*.yaml"

# Discovery metadata (shown by tpm search and tpm info)
meta:
  commands: []
  keywords: ["example"]
`;
}

function tagsTemplate() {
  return `# Tag declarations for this pack.
# Tags listed here can be used on entities (items, npcs, rooms, areas).
# Undeclared tags cause load failure when tag_validation: strict.
#
# Convention: always snake_case (e.g., safe_recall, not safe-recall)
# applies_to: which entity types accept this tag
#   valid values: item, npc, room, area, player
#
# Engine tags from @tapestry/core (like killable, no_kill, persistent)
# are available to all packs without declaring them here.
# Tags below are YOUR pack's custom tags.
tags:
  safe_recall:
    description: "Room is a safe recall destination with no combat"
    applies_to: [room]

  example_tag:
    description: "An example tag -- replace or remove this"
    applies_to: [item]

  # More examples:
  # cursed:
  #   description: "Item carries a curse -- must be removed before unequipping"
  #   applies_to: [item]
  # vendor:
  #   description: "NPC offers specialized trade goods"
  #   applies_to: [npc]
`;
}

function areaTemplate() {
  return `# Area definition -- one per folder.
# Areas group rooms, mobs, and items into a named zone.
area:
  id: example-area             # unique within this pack, no spaces
  name: "Example Area"         # human-readable name shown in-game
  level_range: [1, 5]          # suggested mob level range for this zone
  reset_interval: 1800         # seconds between mob/item respawns
  occupied_modifier: 3.0       # respawn slows by this factor when players are present
  weather_zone: temperate      # weather pattern (requires @tapestry/weather)
  flags: [safe_recall]         # area-level tags (must be declared in tags.yml)
`;
}

function roomTemplate(shortName) {
  return `# Room definition -- one file per room.
# ID format: "pack-short-name:room-id"  (short name = part after the slash in @scope/name)
id: "${shortName}:town-square"
area: example-area             # must match area.id in area.yaml
name: "Town Square"
description: >
  A cobblestone square at the heart of the example area.
  <npc>A guard</npc> stands watch near the well.
  A <item.uncommon>lantern</item.uncommon> hangs on a hook by the gate.

# Exits: direction -> "pack-name:room-id"
exits:
  north: "${shortName}:another-room"

# Tags must be declared in tags.yml
tags: [safe_recall]

properties:
  terrain: city   # city, forest, plains, dungeon, mountain, water, desert

# Mobs that spawn here on area reset
spawns:
  - mob: "${shortName}:example-guard"
    count: 1
    tags: [persistent]   # persistent = respawns even while players are present

# Items placed in room on reset (not carried by mobs)
fixtures:
  - "${shortName}:example-lantern"
`;
}

function mobTemplate(shortName) {
  return `# NPC (mob) definition -- one file per NPC type.
# ID format: "pack-short-name:mob-id"
id: "${shortName}:example-guard"
name: "a guard"
type: "npc"

# Tags control behavior. no_kill: players cannot attack this NPC.
tags: [no_kill]

# friendly, neutral, hostile -- initial stance toward players
base_disposition: friendly

# Words players type to target this NPC: kill guard, talk guard
keywords: [guard, soldier]

# Optional: links to a behavior script in scripts/mobs/<behavior>.js
# behavior: patrol

stats:
  strength: 12
  dexterity: 10
  constitution: 12
  intelligence: 8
  wisdom: 8
  luck: 6
  max_hp: 100
  max_resource: 0
  max_movement: 100

properties:
  level: 5
  description: "A guard standing watch near the gate."

# Items equipped on spawn -- must be item IDs in this pack or dependencies
# equipment:
#   - "${shortName}:example-sword"
`;
}

function itemTemplate(shortName) {
  return `# Item definition -- one file per item type.
# ID format: "pack-short-name:item-id"
id: "${shortName}:example-lantern"
name: "a battered lantern"
type: "item"

# Tags must be declared in tags.yml (required when tag_validation: strict)
tags: []

# Words players type to target this item: get lantern, look lantern
keywords: [lantern, light]

properties:
  weight: 2
  rarity: common    # common, uncommon, rare, legendary
  value: 5          # coin value when sold to a shop

# Stat modifiers applied when item is equipped
# modifiers:
#   - stat: strength
#     amount: 2
modifiers: []
`;
}

function initScriptTemplate(scopedName) {
  return `// init.js -- runs when this pack loads.
// Register commands, subscribe to events, declare properties.
// The tapestry object is injected by the engine at load time.

// --- Command registration ---
// Registers a command players can type in-game.
tapestry.commands.register({
    name: 'example',
    aliases: [],
    description: 'An example command from ${scopedName}',
    category: 'general',
    roles: ['player'],
    args: {
        target: { type: 'text', required: false }
    },
    handler: function(actor, resolved) {
        var msg = resolved.target
            ? 'You examine the ' + resolved.target + '.'
            : 'Nothing to examine.';
        actor.send(msg + '\\r\\n');
    }
});

// --- Event subscriptions ---
// Subscribe to events from the engine or other packs.
// Core events: entity:entered_room, entity:left_room,
//   entity:attacked, entity:killed, item:picked_up, item:dropped
//
// tapestry.events.on('entity:entered_room', function(entity, room) {
//     var weather = room.get('weather_current');
//     if (weather === 'blizzard') {
//         entity.send('The cold bites at you as you arrive.\\r\\n');
//     }
// });

// --- Property registration ---
// Declare properties your pack writes to entities.
// Other packs read these via entity.get('your-property').
//
// tapestry.properties.register('example-status', {
//     type: 'string',
//     default: null,
//     applies_to: ['player', 'npc'],
// });
`;
}

function helpTemplate(scopedName) {
  return `# Help file -- documents a command or topic.
# Players read this in-game with: help example
id: "example"
title: "Example Command"
category: "general"      # general, combat, social, building, admin
role: "player"           # player, builder, admin (who can see this help)
keywords: [example, demo]
brief: "An example command from ${scopedName}."
syntax:
  - "example"
  - "example [target]"
body: |
  The example command is a placeholder from the pack scaffold.
  Replace this with documentation for your actual commands.

  Use syntax entries to show all forms of the command.
  Keep help text concise -- players read this at the terminal.
see_also: [help, commands]
`;
}

function generatePackFiles({ scopedName, shortName }) {
  return [
    { path: 'tpm.yaml', content: manifestTemplate(scopedName) },
    { path: 'tags.yml', content: tagsTemplate() },
    { path: 'areas/example-area/area.yaml', content: areaTemplate() },
    { path: 'areas/example-area/rooms/town-square.yaml', content: roomTemplate(shortName) },
    { path: 'areas/example-area/mobs/guard.yaml', content: mobTemplate(shortName) },
    { path: 'areas/example-area/items/lantern.yaml', content: itemTemplate(shortName) },
    { path: 'scripts/init.js', content: initScriptTemplate(scopedName) },
    { path: 'help/example.yaml', content: helpTemplate(scopedName) },
  ];
}

module.exports = { generatePackFiles };
