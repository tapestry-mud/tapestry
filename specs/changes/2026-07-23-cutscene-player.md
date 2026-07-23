---
release: unreleased
specs: [output-pipeline.md]
---

# Cutscene Player

## Why

The prompt-hold gate (slice 1, engine 0.1.52) fixed the swell's per-beat prompt spam by
suppressing the once-per-tick redraw while a hold is open. The same seam - hold the prompt
during paced or scripted output, release and redraw once when the player is expected to act
again - is also exactly what a scripted opener cutscene needs. The output-cadence-cutscenes
design spec designed this as its slice 2: a scripting-facing cutscene player built on the
shipped Layer-1 gate, so content packs (starting with the Weaver first-login opener) can play
a paced beat sequence without any pack ever touching the hold directly.

## What

`CutsceneManager` (`Tapestry.Engine.Cutscene`) is the Layer-2 primitive. `Play(playerId, beats,
skippable, currentTick, onComplete)` opens a prompt hold owned by `"cutscene"`, optionally sends
a skip hint, and emits the first beat immediately. Each beat is `{ Text, PauseAfterTicks }`;
timing is tick-driven, not hand-rolled `schedule` callbacks - `CutscenePulse` advances every
in-flight cutscene each heartbeat tick (same cadence/priority tier as `SwellClockPulse`), and a
beat emits once the tick reaches its scheduled `NextEmitTick`.

`PlayerSession` gains `ActiveCutscene`, consulted in `HandleInput` before the normal command
queue (same position as `CurrentFlow`), so all input during a cutscene is swallowed except the
literal line `skip` - and `skip` only does anything when the cutscene's `skippable` flag is
true; otherwise it is swallowed like any other line. `skip` never discards content: it prints
every remaining beat immediately with zero inter-beat delay, then takes the exact same
completion path as natural playback (release the hold, clear `ActiveCutscene`, fire
`onComplete`) - the terminal state is identical either way, only faster. `onComplete` is
guaranteed exactly once: the stored callback is nulled before invocation.

The hold is never reimplemented, only consumed: every open/release call goes through the
existing `PlayerSession.OpenPromptHold`/`ReleasePromptHold`, so the disconnect/link-death
auto-release wired in slice 1 (`ForceReleaseAllPromptHolds`) already covers a cutscene exactly
like it covers a swell - a mid-cutscene disconnect can never strand a held prompt. Separately,
`AdvanceAll` guards against a subtler case the raw hold release does not: a hard disconnect
followed by a fresh reconnect constructs a brand new `PlayerSession` for the same entity id, so
a stale `CutsceneManager` entry checks that the live session's `ActiveCutscene` still points at
itself before acting, and silently drops otherwise rather than misfiring `onComplete` (a
teleport, for the Weaver opener) against a session that never asked for it.

`tapestry.cutscene.play(playerEntityId, beats, options, onComplete)` is the scripting surface
(`CutsceneModule`, `types/tapestry-engine.d.ts`). `beats` is `[{ text, pauseAfter }, ...]`;
a beat's missing `pauseAfter` falls back to `CutsceneManager.DefaultPauseAfterTicks` (20 ticks,
~2s) at this JS-parsing seam. `options.skippable` defaults false. `tapestry.cutscene.isActive`
exposes live state for content that wants to branch on it.

## Consumer

`threadwalker`'s Weaver first-login opener (game-hub-threads plan, Task 17) is the first
content built on this primitive: a beat sequence played on a player's first-ever connect,
whose `onComplete` teleports them into the hub. See threadwalker's own specs for that wiring.
