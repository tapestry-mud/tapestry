---
capability: telnet-protocol
last-updated: 2026-06-12
---

# Telnet Protocol

## Overview

Tapestry accepts raw TCP (telnet) connections via `TelnetServer`. All telnet
wire-level behavior -- option negotiation, the parse state machine, protocol
routing, and low-level framing -- is confined to `Tapestry.Networking`. The
engine is never exposed to raw telnet bytes; it receives only clean
`IConnection` events and `ClientCapabilities` after negotiation completes.

Session lifecycle (PlayerSession, link-dead, idle timeout, flood protection) is
documented in `specs/sessions-and-connections.md`.

---

## Behavior

### TelnetServer accept loop

- `TelnetServer` binds `IPAddress.Any` on the configured port and loops on
  `AcceptTcpClientAsync`. Each accepted `TcpClient` immediately has `TCP_NODELAY`
  set inside the `TelnetConnection` constructor (NoDelay = true)
  (src/Tapestry.Networking/TelnetConnection.cs:42).
- If keepalive is enabled, `TcpKeepAlive.Apply` is called on the socket before
  any other processing. A `SocketException` from that call is logged as a warning
  and swallowed so it never kills the accept loop
  (src/Tapestry.Networking/TelnetServer.cs:62-75).
- `TelnetServer` keeps an internal `List<TelnetConnection>` protected by a lock.
  Connections register an `OnDisconnected` callback that removes them from the
  list (src/Tapestry.Networking/TelnetServer.cs:101-113).
- On server shutdown, all connections in the list receive
  `Disconnect("server shutdown")`
  (src/Tapestry.Networking/TelnetServer.cs:134-145).
- `ReadLoopAsync` is fired as a background `Task` (not awaited directly); faults
  are logged via a continuation
  (src/Tapestry.Networking/TelnetServer.cs:116-125).
- `OnConnectionAccepted` is raised after negotiation and before the read loop
  starts; `ConnectionHandler` subscribes to this event to begin the login flow
  (src/Tapestry.Networking/TelnetServer.cs:114).

### TelnetNegotiator option negotiation

- `TelnetServer` constructs a `TelnetNegotiator` per connection, passing
  `_negotiationTimeoutMs` (a required constructor parameter with no default at
  the call site) and the handler list built by `BuildHandlers`
  (src/Tapestry.Networking/TelnetServer.cs:83-84).
- `TelnetNegotiator` itself has a default value of 500 ms for `timeoutMs` in its
  own constructor signature, but `TelnetServer` always supplies an explicit value
  from server configuration
  (src/Tapestry.Networking/TelnetNegotiator.cs:23).
- The negotiator opens by sending `IAC DO TTYPE` and `IAC DO NAWS` in one write,
  then calls `NegotiateAsync` on each registered `IProtocolHandler` (MSSP, GMCP)
  (src/Tapestry.Networking/TelnetNegotiator.cs:53-59).
- If the client replies `IAC WILL TTYPE`, the negotiator replies
  `IAC SB TTYPE SEND IAC SE` once (sentTtypeSend guard)
  (src/Tapestry.Networking/TelnetNegotiator.cs:96-103).
- NAWS data is read as two big-endian 16-bit values (width, height) from
  subnegotiation payload bytes 1-4
  (src/Tapestry.Networking/TelnetNegotiator.cs:142-147).
- If the client sends `IAC DO <option>` and a registered handler owns that
  option, `handler.HandleRemoteDo` is called; a `DO GMCP` response additionally
  sets the `gmcpActive` flag
  (src/Tapestry.Networking/TelnetNegotiator.cs:106-109).
- The negotiation loop exits early when both `gotTtypeValue` and `gotNawsData`
  are true, or when the timeout fires
  (src/Tapestry.Networking/TelnetNegotiator.cs:66).
- On `OperationCanceledException` (timeout or cancellation), negotiation builds
  `ClientCapabilities` from whatever was collected; if neither a TTYPE nor a NAWS
  width was received, it falls back to `ClientCapabilities.Default` via
  `ClientCapabilities.FromTimeout()`
  (src/Tapestry.Networking/TelnetNegotiator.cs:161-179).
- If negotiation throws for any reason, `ClientCapabilities.Default` is applied
  and a warning is logged
  (src/Tapestry.Networking/TelnetServer.cs:95-99).

### ClientCapabilities

- `ClientCapabilities.Default` is: 80x24 window, `ColorSupport.None`, server-
  echo on, GMCP off, `IsMudClient` false
  (src/Tapestry.Networking/ClientCapabilities.cs:61-70).
- Color support is derived from TTYPE: if the TTYPE string matches a known MUD
  client name (case-insensitive), `ColorSupport.Extended` is returned; a TTYPE
  containing "TRUECOLOR" yields `TrueColor`; one containing "256COLOR" yields
  `Extended`; any other non-null TTYPE yields `Basic`; no TTYPE yields `None`
  (src/Tapestry.Networking/ClientCapabilities.cs:99-124).
- Known MUD clients (zmud, cmud, mudlet, mushclient, tintin++, tintin,
  blowtorch, atlantis, potato, beip, kildclient, gnome-mud) additionally flip
  `UseServerEcho` to false and `IsMudClient` to true
  (src/Tapestry.Networking/ClientCapabilities.cs:79-81).
- After negotiation, `SetCapabilities` applies the result: if `UseServerEcho` is
  true, `IAC WILL ECHO` is sent and `_echoEnabled` is set; otherwise
  `_echoEnabled` is false and no echo option is sent
  (src/Tapestry.Networking/TelnetConnection.cs:55-68).

### TelnetProtocolRouter

- After negotiation, handlers with `IsSessionLong == true` are registered into a
  `TelnetProtocolRouter` that is attached to the connection for the session
  lifetime
  (src/Tapestry.Networking/TelnetNegotiator.cs:166-171).
- `TelnetProtocolRouter` dispatches subnegotiation payloads by option code. It
  holds a `Dictionary<byte, IProtocolHandler>`; unrecognized option codes are
  silently dropped
  (src/Tapestry.Networking/TelnetProtocolRouter.cs:5-17).
- `TelnetProtocolRouter.Dispose` clears the handler dictionary; this is called
  from `TelnetConnection.Disconnect`
  (src/Tapestry.Networking/TelnetProtocolRouter.cs:29-32,
  src/Tapestry.Networking/TelnetConnection.cs:188).
- `GetHandler<T>(byte option)` allows callers to retrieve a typed handler
  by option code (src/Tapestry.Networking/TelnetProtocolRouter.cs:20-27).

### TelnetProtocolConstants

All option codes and command bytes are defined in `TelnetProtocolConstants`
(src/Tapestry.Networking/TelnetProtocolConstants.cs):

| Constant   | Value | Meaning                          |
|------------|-------|----------------------------------|
| IAC        | 255   | Interpret As Command             |
| NOP        | 241   | No operation (liveness probe)    |
| SB         | 250   | Subnegotiation begin             |
| SE         | 240   | Subnegotiation end               |
| WILL       | 251   | Will enable option               |
| WONT       | 252   | Will not enable option           |
| DO         | 253   | Request peer enable option       |
| DONT       | 254   | Request peer disable option      |
| OPT_ECHO   |   1   | Echo                             |
| OPT_TTYPE  |  24   | Terminal type                    |
| OPT_NAWS   |  31   | Negotiate About Window Size      |
| OPT_MSSP   |  70   | MUD Server Status Protocol       |
| OPT_GMCP   | 201   | Generic MUD Communication Proto  |
| MSSP_VAR   |   1   | MSSP variable separator          |
| MSSP_VAL   |   2   | MSSP value separator             |

### TelnetConnection low-level (wire format and backpressure)

- TCP_NODELAY is set immediately in the constructor; the underlying field is
  `_client.NoDelay = true`
  (src/Tapestry.Networking/TelnetConnection.cs:42).
- `ReadLoopAsync` reads a 1024-byte buffer in a loop and feeds bytes to
  `ProcessInboundBytes`
  (src/Tapestry.Networking/TelnetConnection.cs:193-226).
- The parse state machine (`Normal / Iac / Negotiate / SubnegOption / Subneg /
  SubnegIac`) persists across `ReadAsync` calls so IAC sequences split across
  reads are handled correctly
  (src/Tapestry.Networking/TelnetConnection.cs:22-25).
- Subnegotiation payloads are dispatched to `TelnetProtocolRouter` on `IAC SE`;
  doubled `IAC IAC` inside subneg data is unescaped to a single `0xFF`
  (src/Tapestry.Networking/TelnetConnection.cs:282-296).
- On a newline (`\n`), the buffered line is trimmed of trailing `\r`, fired on
  `OnInput`, and the buffer cleared. `\r` bytes are ignored; backspace
  (0x08 or 0x7F) removes the last character; bytes below 0x20 (other than the
  above) are silently dropped
  (src/Tapestry.Networking/TelnetConnection.cs:307-343).
- If server-echo is active, received printable characters are echoed back as
  received; on backspace, `BS SP BS` (0x08 0x20 0x08) is sent
  (src/Tapestry.Networking/TelnetConnection.cs:329-335).
- Input buffer backpressure: a line longer than 4096 bytes
  (`TelnetConnection.MaxLineLength`) is discarded with a warning message sent
  to the client; an `_inputBuffer` exceeding 65536 bytes
  (`TelnetConnection.MaxBufferSize`) disconnects the connection
  (src/Tapestry.Networking/TelnetConnection.cs:207-212, 337-342).
  The test at tests/Tapestry.Networking.Tests/TelnetConnectionHardeningTests.cs:9-13
  pins these two constants but does not exercise the discard/disconnect behavior.
- `SendSubnegotiation` escapes any `0xFF` bytes in the payload (doubles them)
  before framing as `IAC SB <option> <payload> IAC SE`
  (src/Tapestry.Networking/TelnetConnection.cs:70-93).
- `Disconnect` is idempotent (guarded by `_disconnectFired`); it closes the TCP
  client, disposes the router, and fires `OnDisconnectedWithReason` then
  `OnDisconnected`
  (src/Tapestry.Networking/TelnetConnection.cs:180-191).

### Telnet heartbeat (IAC NOP liveness probe)

- `TelnetConnection.Heartbeat` writes `IAC NOP` (0xFF 0xF1) to the peer. It is
  a liveness probe, not a ping/pong: the write forces a TCP send; against a half-
  open peer the write eventually errors and calls `Disconnect`
  (src/Tapestry.Networking/TelnetConnection.cs:122-135).
- `Heartbeat` uses a dedicated write rather than `SendRawBytes` because
  `SendRawBytes` swallows write errors; the heartbeat's purpose is to surface them
  (src/Tapestry.Networking/TelnetConnection.cs:116-121 comment).
- Tested: `Heartbeat_writes_iac_nop_to_the_peer` confirms the two-byte frame
  `[255, 241]` is delivered to the peer
  (tests/Tapestry.Networking.Tests/TelnetConnectionHardeningTests.cs:149-169).
- The error-to-Disconnect path against a genuinely half-open peer cannot be
  unit-tested deterministically; it is covered via a fake connection in
  `Tapestry.Engine.Tests.HalfOpenDetectionTests` and verified live
  (tests/Tapestry.Networking.Tests/TelnetConnectionHardeningTests.cs:172-176
  comment).
- The GameLoop wiring for the heartbeat tick handler is described in
  `specs/sessions-and-connections.md` (GameLoop connection heartbeat section).

---

## Rejected and Reverted

- None on record.

---

## Change Log
