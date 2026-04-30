# recall

## Scenario: Recall from another room
- Players: Wanderer

### Steps
1. Wanderer: `north`
2. Assert Wanderer sees: `The Wanderer's Rest`
3. Wanderer: `recall`
4. Assert Wanderer sees: `surrounded by a brief flash of light`
5. Wanderer: `look`
6. Assert Wanderer sees: `Town Square`

## Scenario: Recall from starting room
- Players: Wanderer

### Steps
1. Wanderer: `recall`
2. Assert Wanderer sees: `surrounded by a brief flash of light`
3. Assert Wanderer sees: `Town Square`
