# gmcp/room-info

# Phase 16.6: Verifies Room.Info GMCP package is sent on move without corrupting output.
# Room.Info binary subneg arrives in-band with text; verify text output is clean.

## Scenario: Room look output not corrupted after GMCP Room.Info would fire
- Players: Wanderer

### Steps
1. Wanderer: `look`
2. Assert Wanderer sees: `Town Square`
3. Assert Wanderer does not see: `{`
4. Assert Wanderer does not see: `IAC`

## Scenario: Moving north sends Room.Info without corrupting room description
- Players: Wanderer

### Steps
1. Wanderer: `north`
2. Assert Wanderer sees: `[Exits:`

## Scenario: Move in valid direction shows arrival room name
- Players: Wanderer

### Steps
1. Wanderer: `look`
2. Wanderer: `north`
3. Assert Wanderer sees: `Wanderer's Rest`
