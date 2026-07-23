---
capability: output-pipeline
last-updated: 2026-07-23
---

# Output Pipeline

## Overview

Every string sent to a player passes through an ordered decorator chain before hitting the
transport. The chain is: ColorRenderingConnection -> TeeConnection -> WrappingConnection ->
raw transport (TelnetConnection or WebSocketConnection). Color tags are resolved first so
the word-wrapper measures ANSI escape sequences as zero-width. The TeeConnection sits between
color and wrap so admin watchers receive color-rendered, unwrapped output. A separate
notification queue accumulates priority-ordered deferred messages (quest alerts, achievement
banners) that are drained once per game tick.

## Behavior

### Connection chain

- **Chain assembly:** `OutputChainFactory.Build` constructs the three decorators in a single
  call site so both telnet and WebSocket connections receive an identical chain: wrapping is
  innermost (closest to transport), then tee, then color rendering outermost
  (src/Tapestry.Server/OutputChainFactory.cs:26-36).

- **ColorRenderingConnection:** Intercepts `SendLine` and `SendText`. Calls
  `ColorRenderer.RenderAnsi` when `IConnection.SupportsAnsi` is true, otherwise
  `ColorRenderer.RenderPlain`. `SupportsAnsi` is delegated to the inner connection unchanged
  (src/Tapestry.Engine/Color/ColorRenderingConnection.cs:17-30).
  Test: `ColorRenderingConnectionTests.SendLine_AnsiCapable_RendersAnsi`,
  `SendLine_PlainClient_StripsTagsToPlainText`.

- **WrappingConnection:** Wraps already-rendered output. Width is resolved lazily on every
  write via `OutputWidthResolver.Resolve`, so a player's `screen_width` preference takes
  effect immediately without rebuilding the chain. A resolved width of 0 or below disables
  wrapping (src/Tapestry.Engine/Text/WrappingConnection.cs:30-31,
  src/Tapestry.Server/OutputWidthResolver.cs:15-19).

- **TeeConnection:** Forwards every write to the player and, when broadcast is not suppressed,
  mirrors the same text to all connected watchers registered under that entity's stable Guid.
  The owner is resolved lazily, so the tee works correctly before and after login
  (src/Tapestry.Engine/Watch/TeeConnection.cs:33-56).

### Color rendering

- **Tag syntax -- semantic:** `<tagname>text</tagname>` where `tagname` is registered in
  `ThemeRegistry`. Unknown angle-bracket sequences pass through as literal text
  (src/Tapestry.Engine/Color/ColorRenderer.cs:119-158).

- **Tag syntax -- literal:** `<color fg="name" bg="name">text</color>` resolves `fg` and `bg`
  directly from the ANSI color tables (ThemeRegistry.ResolveFgColor / ResolveBgColor)
  without requiring a registered theme entry
  (src/Tapestry.Engine/Color/ColorRenderer.cs:87-115,202-233).

- **Tag syntax -- brace shorthand:** `{colorname}` maps to a fixed inline table of 16 standard
  colors plus `bold`, `dim`, `{/}`, and `{reset}`. Case-insensitive. Unrecognized brace
  tokens pass through as literal text
  (src/Tapestry.Engine/Color/ColorRenderer.cs:12-34,163-178).

- **Strip mode:** When `strip: true` (plain clients), all three tag forms are consumed and
  stripped; only the inner visible text is emitted
  (src/Tapestry.Engine/Color/ColorRenderer.cs:59,109,154,171).

- **Result cache:** Both `RenderAnsi` and `RenderPlain` cache results in a
  `ConcurrentDictionary` keyed on the raw input string. The cache is populated once per unique
  string; repeated sends of the same tagged string pay zero re-render cost
  (src/Tapestry.Engine/Color/ColorRenderer.cs:8-9,43-47,51-56).

### Theme registry

- **Registration and compile:** Packs call `ThemeRegistry.Register(tag, ThemeEntry{Fg, Bg})`
  during the commit scope; `Compile()` freezes entries into `Dictionary<string, AnsiPair>`
  where `AnsiPair.Open` concatenates the resolved fg+bg ANSI codes and `AnsiPair.Close` is
  always `ESC[0m`. `Fg`/`Bg` names must match the 16-color built-in table (including
  bright and dark-gray aliases); unknown names produce no code and the entry is dropped
  (src/Tapestry.Engine/Color/ThemeRegistry.cs:57-84).
  Test: `ThemeRegistryTests.Register_AndResolve_ReturnsAnsiPair`, `FgAndBg_Combined`.

- **IsKnown:** Checks the pre-compile `_entries` map (not the compiled map), so it returns
  true immediately after `Register` even before `Compile`. `ColorRenderer` uses it to gate
  semantic tag expansion (src/Tapestry.Engine/Color/ThemeRegistry.cs:91-94).
  Test: `ThemeRegistryTests.IsKnown_DoesNotRequireCompile`.

### Word wrapping

- **Algorithm:** `OutputWrapper.Wrap` splits input on existing newlines, then runs a single
  forward pass per segment tracking visible columns and the most recent space (break
  opportunity). ANSI CSI escape sequences (`ESC [` ... final byte) are measured as zero visible
  columns. A token longer than the entire width is emitted intact (overflow rather than split)
  (src/Tapestry.Engine/Text/OutputWrapper.cs:31-131).
  Test: `OutputWrapperTests.OverLongWord_OverflowsWhole_NotSplit`,
  `OutputWrapperTests.AnsiEscapes_AreZeroWidth`.

- **Width resolution:** `OutputWidthService.Resolve(entity)` uses the player's `screen_width`
  property when set and positive; otherwise the server-configured `output.wrap_width`. Result
  is clamped to [MinWidth=20, MaxWidth=500]. A preference of 0 or negative returns 0 (wrap
  disabled). Pre-login (null player) returns the configured default
  (src/Tapestry.Engine/Text/OutputWidthService.cs:24-44).
  Test: `OutputWidthServiceTests.Pref_ClampedMin`, `OutputWidthServiceTests.Pref_Zero_Off`,
  `OutputWidthServiceTests.NullEntity_UsesDefault`.

- **Shared source of truth:** `UiModule` and `WrappingConnection` both call `OutputWidthService`
  so prose wrapping and panel frame widths always agree on the player's effective width
  (src/Tapestry.Engine/Text/OutputWidthService.cs:8-11,
  src/Tapestry.Scripting/Modules/UiModule.cs:44-46).

### Notification queue

- **Enqueue / Drain:** `NotificationQueue.Enqueue(entityId, Notification)` appends a
  `Notification` (Type, Priority, Text, GmcpPackage?, GmcpPayload?) to a per-entity
  `ConcurrentQueue`. `DrainFor(entityId)` dequeues all pending items, sorts ascending by
  `Priority` (lower = higher urgency), clears the queue, and returns the sorted list. Within
  the same priority, order is not guaranteed -- `List<T>.Sort`, which is used internally, is
  documented as unstable. A drain on an unknown entity is a no-op
  (src/Tapestry.Engine/NotificationQueue.cs:14-43).
  Tests: `NotificationQueueTests.DrainFor_SortsByPriority_LowerFirst`,
  `DrainFor_SamePriority_PreservesInsertionOrder`, `DrainFor_EmptiesQueue_SecondCallReturnsEmpty`,
  `DrainFor_SeparateEntities_IndependentQueues`.

- **Drain timing:** The game loop drains all active sessions once per tick. Each notification's
  `Text` is sent to the player via `SessionManager.SendToPlayer`; items with a `GmcpPackage`
  are forwarded to `NotificationHandler`, which calls `IGmcpConnectionManager.Send` (GMCP
  internals are in gmcp.md) (src/Tapestry.Server/GameLoopService.cs:281-293,
  src/Tapestry.Server/Gmcp/Handlers/NotificationHandler.cs:26-35).
  Test: `NotificationHandlerTests.DrainAndSend_SkipsNotifications_WithoutGmcpData`.

- **JS API:** `tapestry.notifications.enqueue(entityId, type, priority, text)`. `GmcpPackage`
  and `GmcpPayload` on `Notification` can only be set from C#; no pack-JS path exists to set
  them (`NotificationsModule.cs` never sets these fields)
  (src/Tapestry.Scripting/Modules/NotificationsModule.cs:21-28).

### Prompt rendering

- **Prompt append and flush:** After any content is sent to a session,
  `SendContentToSession` sets `NeedsPromptRefresh = true` on that session. If a prompt was
  already on screen and no new input has arrived, it also injects a bare `\r\n` before the
  content to push the stale prompt off the line. At the end of each game-loop tick,
  `FlushPrompts` iterates every active, logged-in session that is not mid-flow or in prompt
  input mode; for each session where `NeedsPromptRefresh` is true it calls
  `PromptRenderer.Render`, which expands `{token}` placeholders (hp, mana, mv, gold, etc.)
  from the player's live stats, then sends `\r\n` + the rendered prompt string. After sending,
  `NeedsPromptRefresh` is cleared and `PromptDisplayed` is set so the next content write
  knows to push the prompt off-line before it outputs
  (src/Tapestry.Engine/PlayerSession.cs:383-393,439-457,
  src/Tapestry.Engine/Prompt/PromptRenderer.cs:17-39).

- **Prompt hold:** A per-session owner-keyed suppression of that once-per-tick redraw, for
  paced output (a boss swell) that would otherwise draw a prompt between every beat.
  `PlayerSession` tracks a set of hold owners; `IsPromptHeld` is the set being non-empty.
  `FlushPrompts` skips a held session before the `NeedsPromptRefresh` check, so it renders
  nothing and does not touch `PromptDisplayed` - beats flow with normal line breaks and the
  cursor-bump fires only on the first beat. `OpenPromptHold(owner)` is idempotent;
  `ReleasePromptHold(owner)` removes one owner and, when it was the last, arms exactly one
  redraw defensively so a hold ending with no trailing content still restores the prompt;
  an unknown owner is a no-op. `ForceReleaseAllPromptHolds()` clears every owner and arms
  one redraw, called on session teardown so a hold can never outlive its session
  (src/Tapestry.Engine/PlayerSession.cs:53-83,478;
  src/Tapestry.Server/GameLoopService.cs:133,159).

### Cutscene player (Layer 2, built on the prompt hold)

- **Beat sequence, engine-paced:** `CutsceneManager.Play(playerId, beats, skippable, currentTick,
  onComplete)` opens a prompt hold owned by `"cutscene"`, sends an optional skip hint, and emits
  the first beat immediately. Each further beat carries its own `PauseAfterTicks`; a beat missing
  one gets `CutsceneManager.DefaultPauseAfterTicks` (20 ticks, ~2s) at the scripting seam, not in
  the engine record itself (src/Tapestry.Engine/Cutscene/CutsceneManager.cs:23-27,48-88;
  src/Tapestry.Scripting/Modules/CutsceneModule.cs:75-89).

- **Tick-driven pacing:** `CutscenePulse` (same cadence/priority tier as `SwellClockPulse`) calls
  `CutsceneManager.AdvanceAll(tick)` every heartbeat tick; a beat emits once the current tick
  reaches its scheduled `NextEmitTick`. No hand-rolled `schedule` callback drives per-beat pacing
  (src/Tapestry.Engine/Heartbeat/CutscenePulse.cs; src/Tapestry.Engine/Cutscene/CutsceneManager.cs:90-119).

- **Input swallow and skip:** `PlayerSession.ActiveCutscene` is set for the duration; `HandleInput`
  routes every line to it before the normal command queue, so all input during a cutscene is
  swallowed except the literal line `skip`, and only when the cutscene's `skippable` flag is true
  (src/Tapestry.Engine/PlayerSession.cs:186-198; src/Tapestry.Engine/Cutscene/CutsceneManager.cs:137-160).

- **Skip flushes, never discards:** `skip` prints every remaining beat's text immediately with
  zero inter-beat delay, then takes the identical completion path as natural playback - terminal
  state is the same either way, only faster (src/Tapestry.Engine/Cutscene/CutsceneManager.cs:162-171).

- **Completion is exactly-once:** both the natural path (the last beat's emission) and the skip
  path route through the same `Complete`, which marks the instance completed, clears
  `PlayerSession.ActiveCutscene`, releases the prompt hold, and invokes `onComplete` once, nulling
  the stored callback first so a second call can never re-fire it
  (src/Tapestry.Engine/Cutscene/CutsceneManager.cs:173-183).

- **Reuses the Layer-1 hold, never reimplements it:** every hold open/release goes through the
  existing `PlayerSession.OpenPromptHold`/`ReleasePromptHold`, so `ForceReleaseAllPromptHolds` on
  disconnect/link-death (already wired for every hold owner) covers a cutscene exactly like it
  covers a swell. `AdvanceAll` additionally checks that the live session's `ActiveCutscene` still
  points at the ticking instance before acting, so a hard disconnect followed by a fresh reconnect
  (a new `PlayerSession` object for the same entity id) silently drops the stale instance instead
  of misfiring its `onComplete` against the new session
  (src/Tapestry.Engine/Cutscene/CutsceneManager.cs:99-108).
  Tests: `CutsceneManagerTests` (Play/AdvanceAll/skip/skippable-false/hold lifecycle),
  `CutscenePulseTests`, `CutsceneModuleTests` (JS beat/options/onComplete parsing).

### Messaging bridge (pack JS to output)

- **Send to player:** `ApiMessaging.Send(entityId, text)` calls
  `SessionManager.SendToPlayer`, then -- unless the entity's `CommandResponseContext` is
  suppressed -- also mirrors the stripped text to GMCP `Response.Feedback` if the client
  supports the `Response` package (src/Tapestry.Scripting/Services/ApiMessaging.cs:44-71).

- **Private send:** `ApiMessaging.SendPrivate(entityId, text)` wraps the call in
  `WatchBroadcastScope.Run`, suppressing tee mirroring for that write only. The player still
  receives text and GMCP normally; spectators do not see it
  (src/Tapestry.Scripting/Services/ApiMessaging.cs:79-82,
  src/Tapestry.Engine/Watch/WatchBroadcastScope.cs:22-36).

- **Room and broadcast sends:** `SendToRoom`, `SendToRoomExcept`, `SendToAll`, and
  `SendToRoomSkipSleeping` delegate to `SessionManager`. `SendToRoomSkipSleeping` additionally
  gates on the target entity's `rest_state` property, skipping sleeping players
  (src/Tapestry.Scripting/Services/ApiMessaging.cs:85-148).

- **JS respond module:** `tapestry.respond(entityId, type, message, category)` sends a GMCP
  `Response.Feedback` packet directly, bypassing the text channel. `respond.suppress(entityId)`
  silences the automatic feedback mirror for the remainder of the command dispatch; the
  suppression flag is cleared in the dispatcher's finally block
  (src/Tapestry.Scripting/Modules/RespondModule.cs:25-66,
  src/Tapestry.Scripting/Modules/CommandsModule.cs:424-427).

### Admin observation (TeeConnection)

- **Per-write gate:** `WatchBroadcastScope.Suppressed` (an `AsyncLocal<bool>`) is checked on
  every `Broadcast` call. A producer wraps private writes in `WatchBroadcastScope.Run`; the
  scope is re-entrant and restores the prior state on exit
  (src/Tapestry.Engine/Watch/WatchBroadcastScope.cs).

- **Watcher delivery:** Only connected sinks receive mirrored output; disconnected sinks are
  skipped but not removed (src/Tapestry.Engine/Watch/TeeConnection.cs:50-55).
  `TeeConnection.ShouldBroadcast` (default true) is a per-connection kill switch; currently
  unused but present as a named seam (src/Tapestry.Engine/Watch/TeeConnection.cs:23).

### UI panel rendering

- **Panel pipeline:** `tapestry.ui.panel(spec)` renders a structured panel to a tagged string
  using the same semantic tag syntax (`<frame>`, `<title>`, `<subtle>`) that `ColorRenderer`
  resolves. The string enters the output chain normally after the script returns it
  (src/Tapestry.Engine/Ui/PanelRenderer.cs:13-49, src/Tapestry.Scripting/Modules/UiModule.cs:49-109).

- **Width cap:** When the panel spec includes `forEntity`, `UiModule.CapWidth` clamps the
  panel's preferred width to the player's effective width (via `OutputWidthService`) without
  going below the panel's minimum renderable width
  (src/Tapestry.Scripting/Modules/UiModule.cs:27-29,98-109).

- **Cell-row border fill:** Every row type pads its content to the full panel inner width
  before the closing frame border. A cell row whose fixed cells do not fill the width (no
  fill cell) has the unclaimed width padded by the renderer, so a grid of fixed-width cells
  (e.g. a keyword chip grid) keeps its right border aligned at the panel edge. A row that
  includes a fill cell already consumes the full width, so the padding is a no-op for it.
  (src/Tapestry.Engine/Ui/PanelRenderer.cs:126-176; tests/Tapestry.Engine.Tests/Ui/PanelRendererTests.cs:476-499)

## Rejected and Reverted

- None on record.

## Change Log

- 2026-07-23 [cutscene-player](changes/2026-07-23-cutscene-player.md) - Layer-2 scripting-facing cutscene player on the prompt-hold gate: paced beats, skip-flushes-not-discards, exactly-once onComplete
- 2026-07-22 [prompt-hold-gate](changes/2026-07-22-prompt-hold-gate.md) - owner-keyed prompt hold suppresses the once-per-tick redraw for paced output; `FlushPrompts` skips held sessions, force-released on session teardown
- 2026-06-18 [command-catalog-display](changes/2026-06-18-command-catalog-display.md)
