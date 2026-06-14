---
release: v0.1.31
specs: [sessions-and-connections.md]
---

# New-character teardown

## Why

A character created through the chargen flow was promoted to `Playing` in place
by `FlowEngine`, which never installed the disconnect teardown that a normal
login gets from `PlayerSpawner.CompleteLogin`. The new player's very first quit
enqueued no `DisconnectEvent`, so the session was never removed from
`SessionManager`: it leaked until restart, and a reconnect with the same name was
met with the session-takeover prompt. Only brand-new characters were affected;
returning players logged in through `CompleteLogin` and tore down normally.

## What

- `NewCharacterTeardownBridge` subscribes to the `character.created` event that
  `FlowEngine` publishes when creation completes and installs the standard
  disconnect teardown on the new session's connection. One subscription covers
  both the telnet and the web pre-auth creation paths.
- `ConnectionTeardown.Wire` is extracted as the single source of truth for the
  teardown; `CompleteLogin` now routes through it too, so the login and
  new-character paths cannot drift.
