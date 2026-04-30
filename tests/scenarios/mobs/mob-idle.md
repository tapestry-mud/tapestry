# mobs/mob-idle

# Phase 17: Idle commands. Mobs with idle_commands speak/emote at random intervals.

## Scenario: Mob with idle_commands speaks in room with players
- Players: Alice
- Room: same as guide mob

### Steps
1. Alice: `admin spawn example-pack:guide`
2. Assert Alice sees: `Spawned`
3. Alice: `look`
4. Assert Alice sees: `the town guide`

## Scenario: Say command still works after idle command system loads
- Players: Alice, Bob
- Room: same

### Steps
1. Alice: `say Idle system smoke test`
2. Assert Bob sees: `Alice says`
3. Assert Bob sees: `Idle system smoke test`
