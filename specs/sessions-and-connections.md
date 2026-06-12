---
capability: sessions-and-connections
last-updated: 2026-06-12
---

## Overview

Tapestry accepts player connections over two transports: raw TCP (telnet) and
WebSocket. Both implement the `IConnection` interface and produce a
`PlayerSession` once the login flow completes. The engine side is transport-
agnostic; all transport-specific behavior is confined to `Tapestry.Networking`
and the `ConnectionHandler` entry point in `Tapestry.Server`.

NOTE: this file is 260+ lines -- split into `flood-protection.md` and
`connection-lifecycle.md` is warranted once either grows further.

---

## Behavior

### Telnet connection acceptance

- `TelnetServer` binds `IPAddress.Any` on the configured port and loops on
  `AcceptTcpClientAsync`. Each accepted `TcpClient` immediately has TCP_NODELAY
  set on the resulting `TelnetConnection`
  (src/Tapestry.Networking/TelnetConnection.cs:43).
- If keepalive is enabled, `TcpKeepAlive.Apply` is called on the socket before
  any other processing. A `SocketException` from that call is logged as a warning
  and swallowed so it never kills the accept loop
  (src/Tapestry.Networking/TelnetServer.cs:62-75).
- `TelnetServer` keeps an internal `List<TelnetConnection>` protected by a lock.
  Connections register an `OnDisconnected` callback that removes them from the
  list (src/Tapestry.Networking/TelnetServer.cs:101-113).
- On server shutdown, all connections in the list receive `Disconnect("server
  shutdown")` (src/Tapestry.Networking/TelnetServer.cs:134-145).

### Telnet protocol negotiation

- Immediately after accepting, `TelnetNegotiator.NegotiateAsync` is called with
  a configurable timeout (default parameter name `negotiationTimeoutMs`)
  (src/Tapestry.Networking/TelnetServer.cs:83-84).
- The negotiator opens by sending `IAC DO TTYPE` and `IAC DO NAWS` in one write,
  then sends the negotiate-request of each registered `IProtocolHandler` (MSSP,
  GMCP) (src/Tapestry.Networking/TelnetNegotiator.cs:53-59).
- If the client replies `IAC WILL TTYPE`, the negotiator replies
  `IAC SB TTYPE SEND IAC SE` once (src/Tapestry.Networking/TelnetNegotiator.cs:96-103).
- NAWS data is read as two big-endian 16-bit values (width, height)
  (src/Tapestry.Networking/TelnetNegotiator.cs:142-147).
- If the client sends `IAC DO <option>` and a registered handler owns that
  option, `handler.HandleRemoteDo` is called; a `DO GMCP` response sets the
  `gmcpActive` flag (src/Tapestry.Networking/TelnetNegotiator.cs:106-109).
- The negotiation loop exits early when both `gotTtypeValue` and `gotNawsData`
  are true, or when the timeout fires (src/Tapestry.Networking/TelnetNegotiator.cs:66).
- On `OperationCanceledException` (timeout), negotiation builds capabilities
  from whatever was collected; falls back to `ClientCapabilities.Default` if
  nothing was gathered (src/Tapestry.Networking/TelnetNegotiator.cs:161-179).
- `ClientCapabilities.Default` is: 80x24 window, no ANSI color, server-echo on,
  GMCP off (src/Tapestry.Networking/ClientCapabilities.cs:61-70).
- Color support is derived from TTYPE: known MUD client names yield
  `ColorSupport.Extended`; a TTYPE containing "TRUECOLOR" yields `TrueColor`;
  "256COLOR" yields `Extended`; any other TTYPE yields `Basic`; no TTYPE yields
  `None` (src/Tapestry.Networking/ClientCapabilities.cs:99-124).
- Known MUD clients (zmud, cmud, mudlet, mushclient, etc.) additionally flip
  `UseServerEcho` to false and `IsMudClient` to true
  (src/Tapestry.Networking/ClientCapabilities.cs:79-81).
- After negotiation, handlers with `IsSessionLong == true` are registered into a
  `TelnetProtocolRouter` that is attached to the connection for the session
  lifetime (src/Tapestry.Networking/TelnetNegotiator.cs:166-171).
- If negotiation throws for any reason, `ClientCapabilities.Default` is applied
  and a warning is logged (src/Tapestry.Networking/TelnetServer.cs:96-99).

### Telnet parse state machine

- `TelnetConnection.ReadLoopAsync` reads a 1024-byte buffer in a loop and feeds
  bytes to `ProcessInboundBytes`
  (src/Tapestry.Networking/TelnetConnection.cs:193-226).
- The parse state machine (`Normal / Iac / Negotiate / SubnegOption / Subneg /
  SubnegIac`) persists across `ReadAsync` calls so IAC sequences split across
  reads are handled correctly (src/Tapestry.Networking/TelnetConnection.cs:22-25).
- Subnegotiation payloads are dispatched to `TelnetProtocolRouter` on `IAC SE`;
  doubled `IAC IAC` inside subneg data is unescaped to a single `0xFF`
  (src/Tapestry.Networking/TelnetConnection.cs:282-296).
- On a newline (`\n`), the buffered line is trimmed of trailing `\r`, fired on
  `OnInput`, and the buffer cleared. `\r` bytes are ignored; backspace (0x08/0x7F)
  removes the last character (src/Tapestry.Networking/TelnetConnection.cs:307-331).
- Input buffer cap: a line longer than 4096 bytes is discarded with a warning
  message to the client; an `_inputBuffer` exceeding 65536 bytes disconnects the
  connection (src/Tapestry.Networking/TelnetConnection.cs:207-212, 337-342).
  Both limits are tested (tests/Tapestry.Networking.Tests/TelnetConnectionHardeningTests.cs:9-13).
- `Disconnect` is idempotent (guarded by `_disconnectFired`); it closes the TCP
  client, disposes the router, and fires `OnDisconnectedWithReason` then
  `OnDisconnected` (src/Tapestry.Networking/TelnetConnection.cs:180-191).

### Telnet heartbeat (keep-alive liveness probe)

- `TelnetConnection.Heartbeat` writes `IAC NOP` (0xFF 0xF1) to the peer. It is
  a liveness probe, not a ping/pong: the write forces a TCP send; against a half-
  open peer the write eventually errors and calls `Disconnect`
  (src/Tapestry.Networking/TelnetConnection.cs:122-135). Tested in
  tests/Tapestry.Networking.Tests/TelnetConnectionHardeningTests.cs:149-169.
- `GameLoop.RegisterHeartbeatHandler` registers a tick handler named
  "connection-heartbeat" that calls `Heartbeat()` on every `LoginPhase.Playing`
  session at the configured interval
  (src/Tapestry.Engine/GameLoop.cs:433-443).
- `GameLoopService` wires this handler when
  `config.Networking.KeepAlive.Enabled && HeartbeatSeconds > 0`; the interval in
  ticks is `round(HeartbeatSeconds * 1000 / TickRateMs)` with a floor of 1
  (src/Tapestry.Server/GameLoopService.cs:200-205).

### WebSocket connection establishment

- `WebSocketConnection` wraps an `System.Net.WebSockets.WebSocket` (ASP.NET
  Core's managed WebSocket). `IsConnected` reflects `WebSocketState.Open`
  (src/Tapestry.Networking/WebSocketConnection.cs:22-23).
- `SupportsAnsi` is always `true` for WebSocket connections
  (src/Tapestry.Networking/WebSocketConnection.cs:27).
- `RunAsync` races a read loop against a write loop; whichever exits first causes
  the other to be abandoned and `Disconnect` to be called
  (src/Tapestry.Networking/WebSocketConnection.cs:104-121).
- Outbound messages are queued in an unbounded `Channel<string>` and serialized
  as JSON `{ type, data }` envelopes by the write loop
  (src/Tapestry.Networking/WebSocketConnection.cs:21, 43-46).
- Inbound frames are accumulated until `EndOfMessage`; a partial message
  exceeding 64 KB disconnects the client
  (src/Tapestry.Networking/WebSocketConnection.cs:167-175).
- Inbound JSON is dispatched on `type`: `"command"` fires `OnInput`; `"gmcp"`
  routes to `WebSocketGmcpHandler`
  (src/Tapestry.Networking/WebSocketConnection.cs:207-228).
- `WebSocketConnection.Heartbeat` is a no-op; keep-alive is delegated entirely
  to ASP.NET Core's built-in WebSocket Ping/Pong frames
  (src/Tapestry.Networking/WebSocketConnection.cs:59-65).
- Echo suppression/restore are no-ops; the web client handles password masking
  locally (src/Tapestry.Networking/WebSocketConnection.cs:67-74).

### Connection handler and session creation

- `ConnectionHandler.HandleNewConnection` wraps the raw `IConnection` in an
  output chain (color renderer, word-wrapper), creates a `LoginContext`, and
  registers it as a pre-login connection in `SessionManager`
  (src/Tapestry.Server/ConnectionHandler.cs:80-94).
- A `LoginFlow` is started on a `Task.Run` thread. Errors from the login flow
  disconnect the raw connection and remove the pre-login record
  (src/Tapestry.Server/ConnectionHandler.cs:102-118).
- `SessionManager` indexes sessions by connection ID, entity ID, player name
  (case-insensitive), and account ID simultaneously
  (src/Tapestry.Engine/PlayerSession.cs:206-211).
- Pre-login connections are tracked in a separate `_preLogin` dictionary keyed by
  connection ID, distinct from fully logged-in sessions
  (src/Tapestry.Engine/PlayerSession.cs:359-374).
- `SessionManager.ConnectionCount` sums pre-login and logged-in counts
  (src/Tapestry.Engine/PlayerSession.cs:381).

### PlayerSession state and lifecycle

- Session starts in `LoginPhase.Creating`
  (src/Tapestry.Engine/PlayerSession.cs:20).
- `LoginPhase.Playing` is the in-world steady state. `LoginPhase.LinkDead` is the
  disconnected-but-retained state.
- `InputQueue` is a `ConcurrentQueue<string>` capped at `MaxQueueDepth = 100`
  (src/Tapestry.Engine/PlayerSession.cs:14, 68-76). The game loop drains at most
  `MaxCommandsPerSessionPerTick = 10` commands per session per tick
  (src/Tapestry.Engine/GameLoop.cs:28, 218).
- `PlayerSession.HandleInput` routes input: if `InputMode.Prompt`, the
  `PromptHandler` callback is invoked; if a `FlowInstance` is active, the flow
  handles it; otherwise `TryConsumeToken` runs flood-gate logic before enqueueing
  (src/Tapestry.Engine/PlayerSession.cs:148-174).

### Idle timeout and AFK kick

- `GameLoop.ConfigureIdleTimeout` converts seconds to ticks using
  `_timer.SecondsToTicks` (src/Tapestry.Engine/GameLoop.cs:129-133).
- The idle-timeout handler runs every 300 ticks (approx. 30 s at 100 ms tick rate)
  (src/Tapestry.Engine/GameLoop.cs:384).
- Admin sessions (checked via `HasRole(adminTag)`) are exempt from idle kicks
  (src/Tapestry.Engine/GameLoop.cs:400).
- Idle check applies only to `LoginPhase.Playing` sessions, never mid-login
  (src/Tapestry.Engine/GameLoop.cs:398).
- Idle ticks are computed as `_tickCount - session.LastInputTick`. A warning
  message is sent once (`IdleWarned` flag) at `_idleWarningTicks`; at
  `_idleTimeoutTicks` the session is enqueued for disconnect
  (src/Tapestry.Engine/GameLoop.cs:404-420).
- `UpdateLastInputTick` resets `IdleWarned` to false
  (src/Tapestry.Engine/PlayerSession.cs:63-66).

### Link-dead handling

- When a `DisconnectEvent` arrives for a `Playing` session and link-dead is
  enabled, the session transitions to `LoginPhase.LinkDead`, gains the
  `"linkdead"` entity tag, `LinkDeadSinceTick` is recorded, and the connection
  is removed from the connection-ID index only (`RemoveConnectionOnly`) leaving
  entity/name lookups intact
  (src/Tapestry.Server/GameLoopService.cs:129-150).
- A room notice is broadcast to the session's room on link-dead entry
  (src/Tapestry.Server/GameLoopService.cs:137-139).
- Intentional quits (reason == "Quit") bypass the link-dead path and go straight
  to full session teardown (src/Tapestry.Server/GameLoopService.cs:127).
- A "linkdead-cleanup" tick handler runs every 300 ticks; it reaps sessions whose
  `LinkDeadSinceTick` age exceeds `LinkDead.TimeoutSeconds`
  (src/Tapestry.Server/GameLoopService.cs:213-265).
- On reap, the player is saved, sessions/entities are unregistered, the room is
  updated, and a `player.logout` event is published
  (src/Tapestry.Server/GameLoopService.cs:226-262).
- `ReplaceConnection` allows a reconnecting player to take over the link-dead
  session; it unhooks `OnInput` from the old connection and hooks the new one
  (src/Tapestry.Engine/PlayerSession.cs:32-35). `ReRegisterConnectionForSession`
  re-adds the new connection ID to the by-connection-ID index
  (src/Tapestry.Engine/PlayerSession.cs:271-273).
- `AllLinkDeadSessions` returns only `_byEntityId` values with phase LinkDead
  (src/Tapestry.Engine/PlayerSession.cs:327-328). Tested:
  tests/Tapestry.Engine.Tests/LinkDead/SessionManagerLinkDeadTests.cs:21-32.
- `FlushPrompts` skips link-dead sessions
  (src/Tapestry.Engine/PlayerSession.cs:444). Tested:
  tests/Tapestry.Engine.Tests/LinkDead/SessionManagerLinkDeadTests.cs:52-66.
- Session takeover guard: a stale `DisconnectEvent` from the old connection is
  dropped if the session's current connection ID no longer matches
  (src/Tapestry.Server/GameLoopService.cs:120-123).

### Flood protection

- `FloodContext` is a value record that carries `FloodProtectionSection`,
  `TicksPerSecond`, a `GetCurrentTick` delegate, an optional logger, and optional
  OTel counters (src/Tapestry.Engine/FloodContext.cs:7-13).
- `FloodProtectionSection` defaults: `CommandsPerSecond = 15`, `BurstSize = 30`,
  `StrikeThreshold = 3`, `StrikeDecaySeconds = 10`
  (src/Tapestry.Data/ServerConfig.cs:302-305).
- Token bucket: `_tokens` is initialized to `BurstSize` on first use; each tick
  elapsed replenishes `CommandsPerSecond * elapsedSeconds` tokens up to
  `BurstSize` (src/Tapestry.Engine/PlayerSession.cs:95-104).
- A command consumes 1 token. If `_tokens < 1`, a "Slow down." message is sent
  once per flood episode (`_floodWarned` flag), a strike is recorded, and the
  command is dropped (src/Tapestry.Engine/PlayerSession.cs:116-130).
- Strikes decay to 0 after `StrikeDecaySeconds` have elapsed since the last
  strike, clearing `_floodWarned` (src/Tapestry.Engine/PlayerSession.cs:107-113).
- When `_floodStrikes >= StrikeThreshold`, the connection is disconnected with
  reason "command flooding" (src/Tapestry.Engine/PlayerSession.cs:134-143).
- If no `FloodContext` is provided to the session constructor, all token checks
  return true unconditionally (src/Tapestry.Engine/PlayerSession.cs:86).

### BadInputTracker

- `BadInputTracker` records unrecognized command verbs via `Record(verb,
  fullInput, playerName, roomId)` (src/Tapestry.Engine/BadInputTracker.cs:21-36).
- Entries are aggregated by lowercase verb in a `ConcurrentDictionary`; each
  entry tracks `Count`, `FirstSeen`, and `LastSeen`
  (src/Tapestry.Engine/BadInputTracker.cs:7, 28-31).
- Each call logs at `Information` level and increments an optional OTel counter
  with a `verb` attribute (src/Tapestry.Engine/BadInputTracker.cs:32-36).
- There is no automatic threshold-based disconnect; the tracker is observability-
  only. No automatic eviction: `Clear()` must be called explicitly
  (src/Tapestry.Engine/BadInputTracker.cs:39-41).

---

## Rejected and Reverted

No tombstones found in the examined git history or source comments for this
capability scope. The 15-commit window shows only additive work (link-dead
helpers, heartbeat, keepalive, cross-buffer IAC parsing, AccountId index).

---

## Change Log

| Date | Author | Summary |
|------|--------|---------|
