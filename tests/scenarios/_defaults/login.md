# Default Login Sequence

The runner substitutes `{PlayerName}` with the actual player name from the scenario.
All test players are pre-seeded in the content pack with password `testpass123`.

### {PlayerName}
1. Wait for: `adventurer?`
2. Send: `{PlayerName}`
3. Wait for: `Password`
4. Send: `testpass123`
5. Wait for: `Welcome`
