# basic-combat

## Scenario: kill command starts combat
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `kill goblin`
2. Assert Wanderer sees: `You attack a goblin!`

## Scenario: kill nonexistent target
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `kill dragon`
2. Assert Wanderer sees: `You don't see 'dragon' here.`

## Scenario: kill with no argument
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `kill`
2. Assert Wanderer sees: `Kill what?`

## Scenario: consider shows difficulty comparison
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `consider goblin`
2. Assert Wanderer sees: `goblin`

## Scenario: consider with alias
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `con goblin`
2. Assert Wanderer sees: `goblin`

## Scenario: consider nonexistent target
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `consider dragon`
2. Assert Wanderer sees: `You don't see 'dragon' here.`

## Scenario: consider with no argument
- Players: Wanderer
- Room: core:training-grounds

### Steps
1. Wanderer: `consider`
2. Assert Wanderer sees: `Consider what?`
