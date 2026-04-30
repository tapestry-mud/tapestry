# mobs/mob-onsay

# Phase 17: onSay hook. Scripted mobs respond to player speech.

## Scenario: Guide responds to help keyword
- Players: Alice
- Room: same as guide mob

### Steps
1. Alice: `admin spawn example-pack:guide`
2. Assert Alice sees: `Spawned`
3. Alice: `say help`
4. Assert Alice sees: `Hello, Alice`

## Scenario: Guide responds to blacksmith keyword
- Players: Alice
- Room: same as guide mob

### Steps
1. Alice: `admin spawn example-pack:guide`
2. Assert Alice sees: `Spawned`
3. Alice: `say where is the blacksmith`
4. Assert Alice sees: `blacksmith is just south`

## Scenario: Guide ignores unmatched speech
- Players: Alice
- Room: same as guide mob

### Steps
1. Alice: `admin spawn example-pack:guide`
2. Assert Alice sees: `Spawned`
3. Alice: `say xyzzy`
4. Assert Alice sees: `You say`

## Scenario: Mob say response does not trigger other mob onSay
- Players: Alice
- Room: same as two guide mobs

### Steps
1. Alice: `admin spawn example-pack:guide`
2. Alice: `admin spawn example-pack:guide`
3. Alice: `say hello`
4. Assert Alice sees: `Hello, Alice`
