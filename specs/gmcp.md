---
capability: gmcp
last-updated: 2026-06-12
---

# GMCP

## Overview

GMCP (Generic MUD Communication Protocol) is the engine's structured out-of-band channel:
JSON payloads tagged with a `Package.Name`, sent alongside normal text output. Over telnet it
rides subnegotiation option 201; over the WebSocket transport it rides typed JSON envelopes.
Server-side package handlers push game state (vitals, room info, quests, ...) on events and in
a deterministic post-login burst; packs send custom packages from JavaScript via
`tapestry.gmcp`. The GMCP feedback mirror is also the engine's accessibility floor: everything
printed to the screen is available as structured data (README.md:162).

## Behavior

- **Telnet negotiation:** During connect, the server offers `IAC WILL GMCP` (option 201)
  (src/Tapestry.Networking/GmcpProtocolHandler.cs:29, TelnetProtocolConstants.cs:18). A client
  `IAC DO GMCP` activates the session (GmcpProtocolHandler.cs:33-37, TelnetNegotiator.cs:106-110).
  Negotiation runs inside a timeout window (500 ms default, TelnetNegotiator.cs:23); the result
  is recorded as `ClientCapabilities.SupportsGmcp` (src/Tapestry.Networking/ClientCapabilities.cs:77-97).
  On a connection that never activated GMCP, `Send` is a silent no-op
  (GmcpProtocolHandler.cs:88, src/Tapestry.Server/Gmcp/GmcpConnectionManager.cs:31).

- **Telnet wire format:** A message is `Package.Name <json>` in UTF-8 inside
  `IAC SB 201 ... IAC SE` (GmcpProtocolHandler.cs:86-94). Outbound JSON is camelCase with null
  properties omitted (GmcpProtocolHandler.cs:10-14). Inbound, a missing JSON body parses as
  `null` and unparseable JSON drops the message (GmcpProtocolHandler.cs:44-61).

- **WebSocket parity:** The web transport wraps GMCP in JSON envelopes -- outbound
  `{ type: "gmcp", package, data }` (src/Tapestry.Networking/WebSocketConnection.cs:235-241),
  inbound the same shape routed to `WebSocketGmcpHandler.HandleIncoming`
  (WebSocketConnection.cs:216-226). WebSocket GMCP is always active and reports every package
  as supported (src/Tapestry.Networking/WebSocketGmcpHandler.cs:10,23).

- **Client package support (`Core.Supports.Set`/`Remove`):** Telnet clients may declare the
  packages they want. Before any `Core.Supports.Set` arrives, every package is treated as
  supported (GmcpProtocolHandler.cs:77). After one, support checks match case-insensitively on
  exact name or prefix (`Char` covers `Char.Vitals`) (GmcpProtocolHandler.cs:78-83). Package
  versions in `Set` entries are parsed and stored but never consulted
  (GmcpProtocolHandler.cs:104-107). `Core.Supports.Remove` deletes entries
  (GmcpProtocolHandler.cs:111-121).

- **Connection registry:** `GmcpConnectionManager` maps connection id to the transport's
  `IGmcpHandler`, and resolves entity-id sends through the session manager
  (src/Tapestry.Server/Gmcp/GmcpConnectionManager.cs:28-48). Handlers register at connection
  accept and unregister on disconnect (src/Tapestry.Server/ConnectionHandler.cs:84-85).

- **Package handlers:** Server-side senders implement `IGmcpPackageHandler` (`Name`,
  `PackageNames`, `Configure`, `SendBurst` -- src/Tapestry.Contracts/IGmcpPackageHandler.cs) and
  are wired through DI (src/Tapestry.Server/Program.cs:140-160) with `Configure()` called once
  at startup to subscribe to engine events (Program.cs:717-721). Packages owned per handler
  (all under src/Tapestry.Server/Gmcp/Handlers/):
  - `Char.Status`, `Char.StatusVars` (CharStatusHandler.cs)
  - `Char.Vitals` (CharVitalsHandler.cs)
  - `Char.Experience` (CharExperienceHandler.cs)
  - `Char.Commands` (CharCommandsHandler.cs)
  - `Char.Effects` (CharEffectsHandler.cs)
  - `Char.Items`, `Char.Equipment` (CharItemsHandler.cs)
  - `Char.Combat.Target`, `Char.Combat.Targets` (CharCombatHandler.cs)
  - `Comm.Channel` (CommHandler.cs)
  - `World.Display.Colors` (DisplayHandler.cs)
  - `World.Time`, `World.Weather` (WorldHandler.cs)
  - `Room.Info`, `Room.Nearby`, `Room.WrongDir` (RoomHandler.cs)
  - `Quest.List`, `Quest.Update`, `Quest.Complete`, `Quest.Abandon` (QuestHandler.cs)
  - `Notification.Show` (NotificationHandler.cs)
  - `Char.Login.Phase`, `Login.Prompt`, `Flow.Step`, `Flow.Help` (LoginHandler.cs)

- **Post-login burst:** On entering play, the orchestrator sends one snapshot per handler in a
  fixed order: Display, CharStatus, CharVitals, CharExperience, CharCommands, CharEffects,
  CharItems, Room, World (src/Tapestry.Server/Gmcp/PostLoginOrchestrator.cs:9-20). It is
  triggered by `LoginHandler.TriggerPostLoginBurst` from the player spawner
  (src/Tapestry.Server/PlayerSpawner.cs:139,194) and on the `character.created` event
  (Handlers/LoginHandler.cs:35-44). The burst is scheduled onto the game loop, after the
  "playing" phase signal (PlayerSpawner.cs:125,139). LoginHandler is deliberately not in the
  DI handler collection to break a circular dependency with the orchestrator
  (Program.cs:159-160). Package set and ordering are asserted end-to-end by
  tests/scenarios/gmcp/gmcp-post-login-burst.md.

- **Vitals batching:** Vitals-changing events (`ability.used`, `entity.regen`,
  `entity.vital.depleted`) mark the entity dirty rather than sending immediately
  (Handlers/CharVitalsHandler.cs:37-53); the `gmcp-vitals-flush` tick handler flushes the
  dirty set once per game tick, one `Char.Vitals` send per dirty entity
  (src/Tapestry.Server/Gmcp/DirtyVitalsBatcher.cs:21-32,
  src/Tapestry.Server/Modules/TickHandlerModule.cs:94).

- **Login-phase signaling:** The login flow emits `Char.Login.Phase` and `Login.Prompt` so a
  structured client can drive login without scraping text
  (src/Tapestry.Server/Login/LoginFlow.cs:463-468,
  src/Tapestry.Server/Login/InteractiveTakeoverConfirmer.cs:54-57). The flow engine exposes a
  `GmcpSend` seam for flow-driven sends (src/Tapestry.Server/ConnectionHandler.cs:74-77).

- **Feedback mirror (accessibility floor):** Every `ApiMessaging.Send` to a player also emits
  a sanitized `Response.Feedback` GMCP message when the client supports the `Response` package
  and the response context has not suppressed it
  (src/Tapestry.Scripting/Services/ApiMessaging.cs:44-71). Packs can send structured feedback
  directly via the `respond` module (src/Tapestry.Scripting/Modules/RespondModule.cs:32-38).
  After every non-mob command the engine also publishes a `communication.message` event
  carrying the command's GMCP channel (default `feedback`); commands opt out with
  `gmcp: false` or set `{ channel, prependSender }`
  (src/Tapestry.Scripting/Modules/CommandsModule.cs:192-208,429-438).

- **Pack JS API:** `tapestry.gmcp.send(entityId, package, payload)`,
  `tapestry.gmcp.supports(entityId, package)`, and `tapestry.gmcp.on(package, callback)`
  (src/Tapestry.Scripting/Modules/GmcpModule.cs:18-50), backed by `GmcpModuleAdapter`
  (src/Tapestry.Server/GmcpModuleAdapter.cs).

- **Inbound messages are currently dropped after parsing:** Both transports raise an
  `OnGmcpMessage` callback for client-sent packages (GmcpProtocolHandler.cs:72,
  WebSocketGmcpHandler.cs:27), but nothing in the server assigns that callback, and
  `GmcpModuleAdapter.DispatchMessage` -- the only path that would fire `tapestry.gmcp.on`
  subscriptions (GmcpModuleAdapter.cs:37-47) -- has no production caller. Net effect:
  client-to-server GMCP only influences `Core.Supports.*` tracking; `tapestry.gmcp.on`
  callbacks never fire. UNVERIFIED: whether this gap is intentional (subscribe-side shipped
  ahead of routing) or a missed wiring.

- **Boundary:** MSSP (telnet option 70, server status for crawlers) is a sibling
  subnegotiation handler on the same negotiator (src/Tapestry.Networking/TelnetServer.cs:151-163,
  tests/scenarios/gmcp/mssp-negotiation.md) but is not part of the GMCP capability.

## Rejected and Reverted

- **Monolithic GmcpService (reverted):** GMCP began as a single 835-line `GmcpService` plus a
  `GmcpEventModule`; both were deleted in favor of per-package DI handlers with
  `Configure()`-time event subscriptions (commit 5445e98).

- **`hungerTier` in `Char.Status` (removed):** The sustenance C# subsystem was extracted to
  pack JS and the derived `hungerTier` field left `Char.Status` with it (commit 37aa677).
  The raw `hungerValue` property remains in the payload, read straight from the entity's
  `sustenance` property (Handlers/CharStatusHandler.cs:83,103); tier derivation is the
  client's job.

## Change Log

| Date | Change Record | Summary |
|------|---------------|---------|
