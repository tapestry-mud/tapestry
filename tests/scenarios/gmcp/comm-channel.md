# gmcp/comm-channel

# Phase 16.6: Comm.Channel GMCP package is sent alongside visible say/gossip output.
# Packs fire tapestry.gmcp.send() per recipient; GmcpService routes to GMCP-capable clients.
# Text output must not be corrupted.

## Scenario: Say text appears normally without GMCP corruption
- Players: Alice, Bob
- Room: same

### Steps
1. Alice: `say Hello Bob`
2. Assert Bob sees: `Alice says`
3. Assert Bob sees: `Hello Bob`
4. Assert Bob does not see: `Comm.Channel`

## Scenario: Gossip text reaches other players without corruption
- Players: Alice, Bob

### Steps
1. Alice: `gossip Testing gossip`
2. Assert Bob sees: `gossip`
3. Assert Bob sees: `Testing gossip`

## Scenario: Emote sends Comm.Channel GMCP (issue #26 fix)
- Players: Alice, Bob
- Room: same

### Steps
1. Alice: `emote waves hello.`
2. Assert Bob sees: `Alice waves hello.`
3. Assert Bob does not see: `Comm.Channel`

