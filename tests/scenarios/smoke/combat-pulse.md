# Combat Pulse

This is the v0.1.19 detector: a tick-handler registration collision once
silently killed the combat pulse — mobs aggroed and players got "in battle"
state, but zero damage ever happened. Unit tests can't see this (it's an
emergent property of the registered tick pipeline), and a hand playtest is
the only other way to catch it. The assertion is the WAIT step: pulse output
(hit OR miss both end with "a training dummy.") must arrive within the wait
window or combat is dead.

## Scenario: Combat pulse produces damage output
- Players: Wanderer, Gamemaster

### Steps
1. Gamemaster: `teleport Gamemaster tapestry-test-fixtures:test-arena`
2. Gamemaster: `spawn tapestry-test-fixtures:test-dummy`
3. Assert Gamemaster sees: `Spawned: a training dummy`
4. Gamemaster: `teleport Wanderer tapestry-test-fixtures:test-arena`
5. Wanderer: `kill dummy`
6. Assert Wanderer sees: `You attack a training dummy!`
7. Wait for Wanderer sees: `a training dummy.`
8. Gamemaster: `purge npc`
9. Assert Gamemaster sees: `Purged`
