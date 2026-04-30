# mobs/mob-onsay

# Phase 17: onSay hook end-to-end. Tests the full pipeline:
# player.say event -> onsay-dispatch.js -> invokeHook -> guide.js -> mob command -> room text.
# Unit tests cover individual components; these verify the wired system works together.

## Scenario: Guide responds to player speech via onSay hook
- Players: Alice, Gamemaster
- Room: same

### Steps
1. Gamemaster: `spawn example-pack:guide`
2. Assert Gamemaster sees: `Spawned`
3. Alice: `say help`
4. Assert Alice sees: `Hello, Alice`

## Scenario: Guide chains commands with delay on blacksmith keyword
- Players: Alice, Gamemaster
- Room: same

### Steps
1. Gamemaster: `spawn example-pack:guide`
2. Assert Gamemaster sees: `Spawned`
3. Alice: `say where is the blacksmith`
4. Assert Alice sees: `blacksmith is just south`
5. Assert Alice sees: `points south`

## Scenario: Mob say response does not trigger other mobs onSay
- Players: Alice, Gamemaster
- Room: same

### Steps
1. Gamemaster: `spawn example-pack:guide`
2. Gamemaster: `spawn example-pack:guide`
3. Alice: `say hello`
4. Assert Alice sees: `Hello, Alice`
