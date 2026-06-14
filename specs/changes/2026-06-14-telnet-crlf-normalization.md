---
release: v0.1.32
specs: [telnet-protocol.md]
---

# Telnet CRLF normalization and per-connection write serialization

## Why

Multi-line telnet output staggered on Linux: the engine emitted lone `\n` in
player-facing body text where the telnet wire wants `\r\n`. A terminal that
receives a bare `\n` drops a line without returning to column 0, so each
successive line stair-steps to the right. The bug was invisible on a Windows
`dotnet run` host because of platform line-ending handling, so it surfaced only
on the deployed Linux server.

Hardening the fix surfaced a second, pre-existing defect: output reaches one
connection from more than one thread (game-loop broadcasts and prompts on the
loop thread; input echo on the read-loop thread), and writes to the connection
were never serialized. The new CRLF state made that race observable, but the
unsynchronized stream write predates this change.

## What

- `TelnetConnection.SendText` normalizes lone `\n` to `\r\n` at the transport
  boundary. It is idempotent (an existing `\r\n` is left alone) and split-chunk
  safe: a per-connection flag tracks whether the previous write ended on a `\r`,
  so a `\r` ending one call and a `\n` opening the next is treated as one pair,
  not doubled to `\r\r\n`.
- Normalization is confined to the player-facing `SendText` path. Raw and
  protocol writes (`SendRawBytes`, the `IAC NOP` heartbeat, IAC negotiation, the
  byte-level echo path) are untouched.
- The WebSocket connection is a separate `IConnection` and is deliberately not
  normalized; web clients interpret `\n` themselves and an injected `\r` would
  corrupt their rendering.
- Every write to a single connection -- `SendText`, `Heartbeat`, and
  `SendRawBytes` -- plus the CR-tracking flag is serialized through a
  per-connection lock. The lock is per connection, so it never contends across
  connections; a blocking write holds it only against other writes to the same
  connection. This also closes the pre-existing torn-output race.

The CRLF normalization carries byte-level unit tests (lone LF, idempotent CRLF,
mixed body, split-chunk boundary, bare CR, and a WebSocket-unchanged assertion).
The write-serialization race does not reproduce on Windows (the same platform
asymmetry as the original bug) and is not separately unit-tested; it rests on the
lock being correct by construction, with the normalization suite proving the lock
is behavior-preserving, and on live verification on the Linux server.
