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
# dependencies:
#   "@scope/pack-name": "^1.0.0"

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

# Discovery metadata (shown by tapestry search and tapestry info)
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
  flags: [safe_recall]         # area flags: city, village, safe_recall, dangerous, safe
  # weather_messages:          # custom weather messages for this area
  #   storm:
  #     start: "Thunder rumbles across the square."
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

# Exits -- simple or with doors
exits:
  north: "${shortName}:another-room"
  # Complex exit with a door:
  # south:
  #   target: "${shortName}:locked-room"
  #   door:
  #     name: "an iron gate"
  #     closed: true
  #     locked: true
  #     key: "${shortName}:iron-key"

# Tags -- engine tags (safe, recall_point, entry_point, no_wander) are from core
# Pack tags must be declared in tags.yml
tags: [safe_recall]

properties:
  terrain: city              # city, indoors, outdoors, forest, underground, road
  # alignment_range:         # restrict entry by alignment
  #   max: -500              # only evil players can enter
  #   alignment_block_message: "A holy barrier repels you."

# Entry point -- marks this room as a starting/recall location
# entry_point_description: "the town square"
# entry_point_direction: south

# Mobs that spawn here on area reset
spawns:
  - mob: "${shortName}:example-guard"
    count: 1
    tags: [persistent]       # persistent = respawns even while players are present

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

# Engine tags: killable, no_kill, shop, skill_trainer, vendor, quest, persistent
# Pack tags must be declared in tags.yml
tags: [no_kill]

# friendly, neutral, hostile -- initial stance toward players
base_disposition: friendly

# Words players type to target this NPC: kill guard, talk guard
keywords: [guard, soldier]

# Behavior -- how the NPC acts when idle
# behavior: stationary       # stationary, wander, patrol, aggro
# script: "mobs/guide.js"   # custom JS behavior script
# patrol_route: ["${shortName}:room-a", "${shortName}:room-b"]
# patrol_interval: 60        # seconds between patrol moves

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
  # gold: 50                  # gold dropped on death
  # xp_value: 100             # XP awarded on kill
  # regen_hp: 2.0             # HP regeneration per tick
  # regen_movement: 5         # movement regeneration per tick
  # corpse_decay: 300         # seconds before corpse disappears

  # Combat properties
  # wimpy_threshold: 15       # % HP to trigger flee
  # ac_slash: 5               # armor class by damage type
  # ac_pierce: 5
  # ac_bash: 5
  # ac_exotic: 0
  # flee_threshold: 0.1       # health % to flee (0.0-1.0)

  # Idle behavior
  # idle_chance: 0.3          # chance to perform idle action per tick
  # idle_interval: 30         # seconds between idle checks

  # Wander behavior
  # wander_interval: 45       # seconds between wander moves
  # wander_boundary: "area"   # area = stay in area, room = stay put

  # Dialogue
  # dialogue: "guard-dialogue" # dialogue tree ID

# Commands the NPC says/does when idle
# idle_commands:
#   - "say All quiet on the watch."
#   - "emote scans the square."

# Commands the NPC uses in combat
# battle_commands:
#   - "bash"
#   - "say You dare challenge me?"

# Abilities the NPC can use
# abilities:
#   - id: "bash"
#     proficiency: 75

# Items equipped on spawn
# equipment:
#   - "${shortName}:iron-sword"

# Loot dropped on death
# loot:
#   guaranteed:
#     - item: "${shortName}:guard-badge"
#       count: 1
#   pool:
#     - item: "${shortName}:health-potion"
#       weight: 10
#     - item: "${shortName}:iron-helm"
#       weight: 3
#   pool_rolls: 1
#   rare_bonus:
#     chance: 0.05
#     pool:
#       - item: "${shortName}:rare-blade"
#         weight: 1

# NPC trains players (skill_trainer tag required)
# trains:
#   tier: "apprentice"
#   abilities: ["bash", "parry"]

# NPC sells items (shop tag required)
# properties:
#   shop:
#     sells: ["${shortName}:health-potion", "${shortName}:iron-sword"]
`;
}

function itemTemplate(shortName) {
  return `# Item definition -- one file per item type.
# ID format: "pack-short-name:item-id"
id: "${shortName}:example-lantern"
name: "a battered lantern"
type: "item"

# Engine tags: consumable, container, fixture, no_get, equippable, fillable,
#   fill_source, readable, emits_light, drinkable, furniture
# Pack tags must be declared in tags.yml
tags: [emits_light]

# Words players type to target this item: get lantern, look lantern
keywords: [lantern, light]

properties:
  weight: 2
  rarity: common             # common, uncommon, rare, epic, artifact
  value: 5                   # coin value when sold to a shop
  # description: "A dented lantern that still glows faintly."
  # level: 1                 # required/recommended level

  # Equipment (equippable tag required)
  # slot: light              # wield, head, feet, shield, finger, neck, hands, cloak, light, held

  # Weapon properties (wield slot)
  # damage_dice: "1d6+2"    # dice notation
  # hit_bonus: 1
  # attack_speed: 3          # lower = faster
  # combat_name: "slash"     # verb for combat messages
  # damage_type: slash        # slash, pierce, bash

  # Armor properties
  # ac_slash: 3
  # ac_pierce: 3
  # ac_bash: 3
  # ac_exotic: 0

  # Container (container tag required)
  # container_capacity: 10

  # Consumable (consumable tag required)
  # consume_method: quaff    # quaff, drink, eat
  # charges: 3
  # max_charges: 3
  # destroy_on_empty: true
  # effect_id: "heal"
  # effect_duration: 10
  # effect_data:
  #   heal_hp: 50
  # sustenance_value: 25     # hunger/thirst satiation

  # Fillable (fillable tag required)
  # fill_type: "water"
  # fill_source: "water"

  # Readable (readable tag required)
  # text: "The inscription reads: 'Welcome to the realm.'"

  # Furniture
  # rest_bonus: 2            # bonus to rest/recovery

  # Magical essence
  # essence: fire            # shadow, fire, earth, storm

# Stat modifiers applied when equipped
# modifiers:
#   - stat: strength         # strength, dexterity, constitution, intelligence,
#     value: 2               #   wisdom, luck, maxHp, maxMovement, maxResource
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
    { path: 'tapestry.yaml', content: manifestTemplate(scopedName) },
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
