# gmcp/char-status

# Phase 16.6: Char.Status GMCP package is sent at login (via initial burst)
# and on level-up. Text output must not be corrupted.

## Scenario: Score command shows character info without corruption
- Players: Alice

### Steps
1. Alice: `score`
2. Assert Alice sees: `Alice`
3. Assert Alice does not see: `Char.Status`

## Scenario: Level display in score not corrupted
- Players: Alice

### Steps
1. Alice: `score`
2. Assert Alice sees: `Level`
3. Assert Alice does not see: `{`
