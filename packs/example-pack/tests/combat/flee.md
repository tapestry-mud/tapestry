# flee

## Scenario: flee exits combat
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `kill goblin`
2. Assert Wanderer sees: `You attack a goblin!`
3. Wanderer: `flee`
4. Assert Wanderer sees: `You flee`

## Scenario: flee when not in combat
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `flee`
2. Assert Wanderer sees: `You're not in combat.`

## Scenario: room sees flee message
- Players: Wanderer, Bystander
- Room: core:training-grounds

### Steps
1. Wanderer: `kill goblin`
2. Assert Wanderer sees: `You attack a goblin!`
3. Wanderer: `flee`
4. Assert Bystander sees: `flees`
