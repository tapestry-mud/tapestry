---
release: v0.1.41
specs: [combat-resolution.md, command-dispatch.md, heartbeat.md, scripting-runtime.md]
---

# Boss Combat Slice 1: The Embedded Swell Loop

## Why

The combat engine had a fast auto-attack loop but no combat clock that could
slow or hold, no notion of a command obeying a combat tempo, and no
validate/resolve seam for a read-and-counter beat. Building a boss whose fight
breathes - a fast chip baseline that decelerates into one telegraphed swing the
player must read and counter, then resumes - required all three as real engine
machinery rather than a per-pack hack. The design goal was one tuneable combat
system, not a separate boss mode: the swell is an embedded beat in the existing
fight, and every difficulty lever lives on the mob as data so a later tier is
config, not code.

## What

- **A command tempo axis.** `CommandRegistration` gains a `Pace` field
  (`Free` | `Battle`, default `Free` so nothing existing changes).
  (src/Tapestry.Engine/Pace.cs:8; src/Tapestry.Engine/CommandRegistry.cs:26)
  `commands.register` marshals `pace: 'free' | 'battle'` off the JS object and
  rejects any other value. (src/Tapestry.Scripting/Modules/CommandsModule.cs:134)

- **A per-fight swell clock.** `SwellClockManager` is a state machine
  (Baseline -> Telegraph -> Window -> Resolve) advanced every tick by
  `SwellClockPulse` (Cadence 1, Priority 90). Any in-combat entity carrying a
  non-empty `swell_window` dial is a swell boss; all timing, content, and
  magnitude dials are read off the mob's properties.
  (src/Tapestry.Engine/Combat/SwellClockManager.cs:10;
  src/Tapestry.Engine/Heartbeat/SwellClockPulse.cs:9) Telegraph emits a
  decelerating stretch wind-up; the `tell` dial chooses how much the wind-up
  reveals (full / shape / hidden).

- **Battle commands obey the clock only while a swell is live.** The router
  intercepts a `Battle`-pace command only when a swell is active for that actor;
  at baseline a battle command dispatches normally. During an active swell a
  counter verb commits the beat, any other combat action is blocked with a nudge,
  and free verbs still fire immediately.
  (src/Tapestry.Engine/CommandRouter.cs:66)

- **The swell suspends the fight's combat actions, not just the auto-attack.**
  Both the auto-attack phase and the ability-resolution phase skip a fight whose
  actor is in an active swell, so neither weapon swings nor queued abilities (a
  cast spell, a bash) leak damage mid-swell; all resume at baseline.
  (src/Tapestry.Engine/Combat/ResolveAutoAttacksPhase.cs:39;
  src/Tapestry.Engine/Heartbeat/AbilityResolutionPhase.cs:65)

- **A validate/resolve seam.** A `WindowValidatorRegistry` stores named,
  deterministic `Func<CombatContext, ValidationResult>` validators registered by
  pack JS through `tapestry.combat.registerWindow`; the clock looks one up by the
  boss's `swell_window` dial and invokes it at resolve. The validator is always
  deterministic code, never the model.
  (src/Tapestry.Engine/Combat/WindowValidatorRegistry.cs:8;
  src/Tapestry.Engine/Combat/CombatContext.cs:24;
  src/Tapestry.Scripting/Modules/CombatModule.cs:259)

- **resolve is the single mutator.** It maps the outcome to a clamped HP change
  read from the boss dials (countered chunks the boss, whiffed/weathered hit the
  player), applies it through the existing stat path, and lets the existing
  `entity.vital.depleted` death event fire - no new death plumbing. Swell beats
  render to the player through a `combat.swell.telegraph|window|resolve` event
  trio. (src/Tapestry.Engine/Combat/SwellClockManager.cs:314;
  src/Tapestry.Server/Modules/SwellEventModule.cs:22)

- The work is additive: free-pace commands, non-swell fights, and the existing
  auto-attack baseline are unchanged. The richer `window` predicate vocabulary
  (typed, double-whiff, threshold, multi-beat), the model proposer/narrator,
  detection, momentum, and groups are reserved on these seams and not built here.
