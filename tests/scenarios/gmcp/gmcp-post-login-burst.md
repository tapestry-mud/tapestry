# GMCP Post-Login Burst

## Scenario: Post-login burst sends all expected packages
- Players: Wanderer

### Steps
1. Assert Wanderer receives GMCP: `World.Display.Colors`
2. Assert Wanderer receives GMCP: `Char.StatusVars`
3. Assert Wanderer receives GMCP: `Char.Status`
4. Assert Wanderer receives GMCP: `Char.Vitals`
5. Assert Wanderer receives GMCP: `Char.Experience`
6. Assert Wanderer receives GMCP: `Char.Commands`
7. Assert Wanderer receives GMCP: `Char.Effects`
8. Assert Wanderer receives GMCP: `Char.Items`
9. Assert Wanderer receives GMCP: `Char.Equipment`
10. Assert Wanderer receives GMCP: `Room.Nearby`
11. Assert Wanderer receives GMCP: `Room.Info`
12. Assert Wanderer receives GMCP: `World.Time`
13. Assert Wanderer receives GMCP: `World.Weather`

## Scenario: World.Display.Colors arrives before Room.Info
- Players: Wanderer

### Steps
1. Assert `World.Display.Colors` packet index is less than `Room.Info` packet index

## Scenario: Room.Nearby arrives before Room.Info
- Players: Wanderer

### Steps
1. Assert `Room.Nearby` packet index is less than `Room.Info` packet index

## Scenario: Login phase signals correct sequence
- Players: Wanderer

### Steps
1. Assert Wanderer receives GMCP: `Char.Login.Phase`
