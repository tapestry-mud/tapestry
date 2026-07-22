---
release: 0.1.52
specs: [output-pipeline.md, combat-resolution.md]
---

# Prompt Hold Gate

## Why

The command prompt is redrawn once per game tick for any session that received output
that tick. Paced output that emits one line per tick - a boss swell's decelerating
telegraph - therefore drew a fresh prompt between every beat, so a sequence meant to read
as one continuous wind-up came across as prompt spam fighting its own cadence. The redraw
decision lives in one transport-agnostic place (`FlushPrompts`), above the render and
telnet/web split, so a single guard there fixes the cadence on both transports at once.

## What

A per-session owner-keyed **prompt hold** suppresses the once-per-tick prompt redraw while
any owner holds it. `PlayerSession` tracks a set of hold owners: `OpenPromptHold(owner)`,
`ReleasePromptHold(owner)`, `ForceReleaseAllPromptHolds()`, and `IsPromptHeld` (the set is
non-empty). `FlushPrompts` gains one guard - a held session is skipped without rendering
and without clearing `PromptDisplayed`, so beats flow with normal line breaks and the
existing cursor-bump fires only on the first beat. The hold is owner-keyed rather than a
bare flag: overlapping owners keep the prompt held until the last one releases, so no
consumer can clear another's hold. Releasing the last owner arms exactly one redraw, so a
hold that ends with no trailing content still brings the prompt back.

Combat wires the gate to the swell arc through the existing server-side event module, so
the swell clock stays a pure event publisher with no session or rendering knowledge. The
module opens the hold on `combat.swell.telegraph` (idempotent across the decel beats) and
releases it on `combat.swell.resolve` after the outcome narration renders - the narration
is the trailing content that drives the single clean redraw. `SwellClockManager` gains one
new event, `combat.swell.abandoned`, published from its existing stale-fight cleanup when a
fight is dropped before ever reaching Resolve (the boss killed outside the swell, or the
player disengaging); the event reads the player id off the fight state directly rather than
the boss entity, which the cleanup allows to be already gone. The module releases the hold
on that event without rendering anything. As a last-resort safety net, session teardown
(`ForceReleaseAllPromptHolds`) clears any lingering hold on link-death and full disconnect,
so a hold can never outlive the session that opened it.

Baseline combat cadence is unchanged - the hold is surgical to the Telegraph-through-Resolve
arc, and outside it the normal per-round prompt (the free-action affordance) still shows.
