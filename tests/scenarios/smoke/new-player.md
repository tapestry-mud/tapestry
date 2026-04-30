# New Player Journey

## Setup
- Players: Wanderer

## Steps
1. Wanderer: `look`
2. Assert Wanderer sees: `Town Square`
3. Wanderer: `help`
4. Assert Wanderer sees: `Tapestry Commands`
5. Wanderer: `north`
6. Assert Wanderer sees: `The Wanderer's Rest`
7. Wanderer: `south`
8. Assert Wanderer sees: `Town Square`
9. Wanderer: `exits`
10. Assert Wanderer sees: `Obvious exits:`
11. Wanderer: `who`
12. Assert Wanderer sees: `Wanderer`
13. Wanderer: `score`
14. Assert Wanderer sees: `Wanderer`
15. Wanderer: `quit`
16. Assert Wanderer sees: `Farewell, adventurer`
