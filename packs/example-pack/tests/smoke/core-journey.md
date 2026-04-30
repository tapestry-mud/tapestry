# Core Pack Journey

A multi-player journey testing tapestry-core commands end-to-end.

## Setup
- Players: Alice, Bob

## Steps
1. Alice: `look`
2. Assert Alice sees: `Town Square`
3. Alice: `who`
4. Assert Alice sees: `Alice`
5. Assert Alice sees: `Bob`
6. Alice: `say Hey Bob, follow me!`
7. Assert Bob sees: `Alice says "Hey Bob, follow me!"`
8. Alice: `north`
9. Assert Bob sees: `Alice leaves north.`
10. Bob: `north`
11. Bob: `look`
12. Assert Bob sees: `Alice is here.`
13. Alice: `say Made it!`
14. Assert Bob sees: `Alice says "Made it!"`
15. Alice: `emote high-fives Bob.`
16. Assert Bob sees: `Alice high-fives Bob.`
17. Alice: `yell HELLO WORLD`
18. Assert Alice sees: `You yell "HELLO WORLD!"`
19. Alice: `recall`
20. Assert Alice sees: `Town Square`
21. Bob: `recall`
22. Assert Bob sees: `Town Square`
23. Bob: `look`
24. Assert Bob sees: `Alice is here.`
