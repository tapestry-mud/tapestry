# gmcp/room-wrong-dir

# Phase 16.6: Room.WrongDir GMCP package fires on invalid movement.
# Text output (error message) must remain clean.

## Scenario: Invalid direction shows error without corruption
- Players: Wanderer
- Room: core:general-store

### Steps
1. Wanderer: `east`
2. Assert Wanderer sees: `cannot go that way`
3. Assert Wanderer does not see: `{`

## Scenario: Moving into a wall shows blocked message
- Players: Wanderer

### Steps
1. Wanderer: `up`
2. Assert Wanderer sees: `cannot go that way`
