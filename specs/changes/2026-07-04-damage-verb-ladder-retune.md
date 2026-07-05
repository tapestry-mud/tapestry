---
release: 0.1.49
specs: [combat-resolution.md]
---

# Damage Verb Ladder Retune

## Why

Travis's first stage-B prod playtest (2026-07-04) with a brand-new level-1
character: every hit with the starter weapon (~6.5 average damage) read
"scratches"/"grazes" for the whole fight. The old ladder's low rungs were
spaced for a damage economy the low-level game does not produce - "hits" only
began at 13, above a level-1 weapon's maximum roll, so the output channel
told a new player their blows were not working even when they were winning.

Design intent, agreed with Travis in-session: verbs key on ABSOLUTE damage -
the verb ladder IS the progression channel. A geared level-1 hit must read
"grazes"/"hits" and a good early roll "injures"; the decorated top tiers stay
gear/spell territory so late-game numbers keep their own vocabulary. The
RELATIVE state of the target (percent HP) is a separate channel - the
condition line emitted by @tapestry/core combat output on band transitions -
and is deliberately not folded into this table.

## What

Retuned the 20 `DamageVerbs` MinDamage boundaries in
`CombatModule` (verbs, decorators, themes, and order unchanged):
tickles 0 / barely scratches 2 / scratches 4 / grazes 6 / hits 9 /
injures 13 / wounds 17 / mauls 22 / decimates 29 / devastates 37 /
MAIMS 47 / MUTILATES 59 / DISMEMBERS 73 / MASSACRES 91 / ANNIHILATES 116 /
OBLITERATES 146 / DESTROYS 191 / PULVERIZES 241 / ERADICATES 301 /
VAPORIZES 421.

Added `DamageVerbLadderTests` (Tapestry.Scripting.Tests) pinning every
boundary and its off-by-one through the public
`tapestry.combat.formatDamageVerb` surface, so the next tuner sees breakage
when the table and the pins diverge.
