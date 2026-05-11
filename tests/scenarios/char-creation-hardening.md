# Character Creation Hardening

## Scenario: Reserved name is rejected with reprompt
- Players: Wanderer

## Login
### Wanderer
1. Wait for: `Speak your name`
2. Send: `self`
3. Wait for: `reserved`
4. Send: `me`
5. Wait for: `reserved`
6. Send: `Admin`
7. Wait for: `reserved`
8. Send: `Wanderer`
9. Wait for: `Password`
10. Send: `testpass123`
11. Wait for: `Welcome`

### Steps
1. Wanderer: `look`
2. Assert Wanderer sees: `The Nexus`
