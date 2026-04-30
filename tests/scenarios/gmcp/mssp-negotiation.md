# gmcp/mssp-negotiation

# Phase 16.6: Verifies MSSP negotiation does not corrupt normal connection or login.

## Scenario: Normal login succeeds with MSSP-capable server
- Players: Alice

### Steps
1. Alice: `look`
2. Assert Alice sees: `Town Square`
3. Assert Alice does not see: `MSSP`

## Scenario: Player can look and see room after MSSP exchange
- Players: Alice

### Steps
1. Alice: `look`
2. Assert Alice sees: `Town Square`
3. Assert Alice does not see: `MSSP`
