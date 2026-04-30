# gmcp/char-vitals

# Phase 16.6: Char.Vitals GMCP package is flushed at end of tick after stat changes.
# Dirty-flag approach: multiple changes in one tick produce one update.
# The binary subneg should not corrupt visible text output.

## Scenario: Score output not corrupted when Char.Vitals fires
- Players: Wanderer

### Steps
1. Wanderer: `score`
2. Assert Wanderer sees: `HP`
3. Assert Wanderer does not see: `{`

## Scenario: HP visible after regen tick
- Players: Wanderer

### Steps
1. Wanderer: `score`
2. Assert Wanderer sees: `HP`

