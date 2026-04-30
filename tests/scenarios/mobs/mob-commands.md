# mobs/mob-commands

# Phase 17: Mob command system. Mobs can execute say and emote commands.
# The mob command produces room text and a Comm.Channel GMCP event.

## Scenario: Mob say produces room text
- Players: Alice
- Room: same as mob

### Steps
1. Alice: `admin spawn example-pack:guide`
2. Assert Alice sees: `Spawned`
3. Alice: `look`
4. Assert Alice sees: `the town guide`

## Scenario: Mob say does not produce text in empty room
- Players: Alice
- Room: different from mob

### Steps
1. Alice: `look`
2. Assert Alice sees: `Town Square`
