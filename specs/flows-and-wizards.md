---
capability: flows-and-wizards
last-updated: 2026-07-22
---

# Flows and Wizards

## Overview

FlowEngine is a general-purpose wizard engine; character creation is its first
consumer, but any pack can drive a multi-step flow. Covers the `tapestry.flows`
JavaScript API, the `FlowDefinition` / `FlowStepDefinition` model, the
`WizardStep` progress indicator, `PlayerCreator`, `FlowPersistenceAdapter`,
and the `IFlowPersistence` interface.

Out of scope: the login state machine that triggers character creation
(login-and-accounts.md), save/load mechanics (persistence.md).

---

## Behavior

### FlowsModule JS API (tapestry.flows)

- Packs access the flow API through the `tapestry.flows` namespace, implemented
  by `FlowsModule`. (src/Tapestry.Scripting/Modules/FlowsModule.cs:28)

#### tapestry.flows.register(definition)

Registers a flow definition at pack load time. Fields:

| Field              | Required | Description |
|--------------------|----------|-------------|
| `id`               | yes      | Unique flow ID (case-insensitive in registry). |
| `trigger`          | yes      | Event name that starts this flow automatically. |
| `display_name`     | no       | Human-readable name for UI display. |
| `cancellable`      | no       | If `true`, the flow can be cancelled mid-run. |
| `steps`            | yes      | Array of step definition objects (see below). |
| `wizard_steps`     | no       | Array of `{id, label}` records for the ANSI progress panel. |
| `on_complete`      | no       | Callback `(entity, scratch) => {success, message}`. Non-object return is treated as success. |
| `override`         | no       | `true` to replace an existing registration without a boot error. |
| `recommend_context`| no       | `"room"` (default) or `"area"` -- selects the recommend context kind. |

(src/Tapestry.Scripting/Modules/FlowsModule.cs:34-127)

- Registrations are deferred through `RegistrationPolicy`; the policy enforces
  that duplicate `id` without `override: true` is a boot error, raised at pack
  resolution (the seal barrier) rather than silently clobbering the earlier
  registration. (src/Tapestry.Scripting/Modules/FlowsModule.cs:119-127)
- The pack name and source file are captured via `ScriptOwner.CurrentPackOwner()`
  and `ScriptOwner.CurrentSourceFile()` at registration time; these read the
  lexically active ES module via `JintActiveModule.ActiveLocation`.
  (src/Tapestry.Scripting/Modules/FlowsModule.cs:44-45; src/Tapestry.Scripting/ScriptOwner.cs:12-17)
- `on_complete` receives `(entity, scratch)`: an entity proxy (`id`, `entityId`,
  `name`, `roomId`, `getProperty`, `setProperty`, `scratch`, `send`) plus the flow's
  scratch store. If the callback returns `{success: false}`, the flow engine calls
  `Restart` during the Creating phase or clears the flow and enqueues `look` in
  other phases. (src/Tapestry.Scripting/Modules/FlowsModule.cs:82-103;
  src/Tapestry.Engine/Flow/FlowEngine.cs:110-145)

#### entity.scratch (flow working memory)

Every step callback and `on_complete` receive the same entity proxy, which carries
a `scratch` handle backed by a per-`FlowInstance` store that lives exactly as long
as the wizard and is NEVER serialized (there is no code path from scratch to
`PlayerSerializer`). Use it for wizard working memory - answers carried between
steps - instead of `entity.setProperty`, which writes the persisted property bag.

- `entity.scratch.get(key)` returns the stored value with its real type preserved
  (a stored bool comes back a bool), or `undefined` if absent.
- `entity.scratch.set(key, value)` stores a value under the flow instance.
- `entity.scratch.has(key)` returns a bool.

There is no `clear`/`remove`: the instance's death is the clear. Real character
fields that belong in the save (`class`, `race`, a registered property) still go
through `entity.setProperty`; the two surfaces sit side by side on the proxy.
(src/Tapestry.Engine/Flow/IFlowScratch.cs;
src/Tapestry.Scripting/Modules/FlowsModule.cs:82-103)

#### tapestry.flows.trigger(entityId, triggerName[, seed])

Starts a new flow for an online entity mid-session by looking up the session
and calling `FlowEngine.Trigger`. No-op if the entity is not online. The optional
`seed` is a plain object copied into the new instance's scratch at creation, so a
command can hand working memory to a flow it triggers before the flow exists (e.g.
`edit area` seeds `{ edit_area: areaId }`). Omitting it starts with empty scratch.
(src/Tapestry.Scripting/Modules/FlowsModule.cs:130-137)

### Step types

All step definitions share a base `FlowStepDefinition` with two optional
fields:

- `skip_if`: callback `(entity, scratch) => bool` -- if it returns `true` the step is
  skipped during `Advance`. (src/Tapestry.Engine/Flow/FlowStepDefinition.cs:6;
  src/Tapestry.Engine/Flow/FlowInstance.cs:291)
- `recommend_field`: string or callback `(entity, scratch) => string` -- names the
  entity property for which the `~` side-action requests an AI suggestion.
  Returning null/empty disables recommend for that field at runtime.
  (src/Tapestry.Engine/Flow/FlowStepDefinition.cs:12;
  src/Tapestry.Scripting/Modules/FlowsModule.cs:297-313)

#### info

Displays text and immediately advances. `text` may be a literal string or a
callback `(entity, scratch) => string`.
(src/Tapestry.Engine/Flow/FlowStepDefinition.cs:15-18;
src/Tapestry.Engine/Flow/FlowInstance.cs:311-314)

#### choice

Presents a numbered list. Fields:

- `prompt`: string or callback.
- `options`: static array or callback `(entity, scratch) => [{label, value, description?,
  tag_line?}]`. `description` may be a string or a callback.
- `on_select`: callback `(entity, scratch, {label, value})`.
- `help_hint`: optional string appended to the help prompt line.

Selection accepts a number or an unambiguous prefix of a label (case-insensitive).
If the resolved options list is empty at runtime the step is treated as a content
misconfiguration: the engine shows "There are no [help_hint] to choose from."
(or a generic message), asks the player to press Enter, and then aborts the flow.
(src/Tapestry.Engine/Flow/FlowStepDefinition.cs:20-28;
src/Tapestry.Engine/Flow/FlowInstance.cs:140-165, 296-307)

#### text

Accepts free-form input. Fields:

- `prompt`: string or callback.
- `validate`: optional callback `(input) => bool`. On failure, `invalid_message`
  is shown (default: "Invalid input. Please try again.").
- `on_input`: callback `(entity, scratch, value)`.
- `secret`: if `true`, echo is suppressed during input.
- `recommend_field`: see base fields above.

When `recommend_field` is set and the player types `~` (optionally followed by a
hint), the engine suspends the step, calls `RecommendBroker` (the engine's
suggestion service, src/Tapestry.Engine/Recommend/RecommendBroker.cs), and presents a
numbered suggestion list. The player picks a number or types their own value.
(src/Tapestry.Engine/Flow/FlowStepDefinition.cs:30-37;
src/Tapestry.Engine/Flow/FlowInstance.cs:167-257)

#### confirm

Presents a `(y/n)` prompt. Fields:

- `prompt`: string or callback.
- `on_yes`: optional callback `(entity, scratch)`.
- `on_no`: optional callback `(entity, scratch)`.

Only `"y"` / `"yes"` or `"n"` / `"no"` (case-insensitive) are accepted; any
other input shows "Please enter y or n." and reprompts.
(src/Tapestry.Engine/Flow/FlowStepDefinition.cs:39-44;
src/Tapestry.Engine/Flow/FlowInstance.cs:259-278)

### WizardStep progress panel

- `WizardStep` is a value record `(StepId, Label)`.
  (src/Tapestry.Engine/Flow/WizardStep.cs:3)
- When a flow definition includes `wizard_steps` and the connection supports
  ANSI, `FlowInstance` clears the screen and renders a 47-column panel for
  each choice step. The panel has a progress row at the top (markers `[>]`
  current, `[*]` done, `[ ]` pending) and a footer with the help hint.
  (src/Tapestry.Engine/Flow/FlowInstance.cs:332-335, 375-481)
- If the progress row would exceed 43 characters it collapses to
  "Step N of M: label". (src/Tapestry.Engine/Flow/FlowInstance.cs:412-414)
- `wizard_steps` only affects rendering; it does not change step execution
  order or add any gating. Steps not referenced by `wizard_steps` are still
  executed normally.

### FlowEngine

- `FlowEngine.Start(session, flowId)` creates a `FlowInstance`, wires
  `OnCompleted` and `OnAborted` callbacks, tracks the entity in
  `PlayerCreator`, and calls `instance.Start`.
  (src/Tapestry.Engine/Flow/FlowEngine.cs:70-91)
- `FlowEngine.Trigger(session, triggerName)` resolves flows by trigger
  name from `FlowRegistry.GetByTrigger`. If multiple flows match the same
  trigger, the last-registered one wins. If no flow is found and the session
  is in `Creating` phase, `FinalizeCreating` is called directly.
  (src/Tapestry.Engine/Flow/FlowEngine.cs:93-108)
- `FinalizeCreating` holds a `_commitLock` and checks `PlayerExists` before
  saving. If the name was taken between reservation and commit, the player
  is disconnected with an explanatory message.
  (src/Tapestry.Engine/Flow/FlowEngine.cs:197-244)
- On successful creation the engine publishes a `character.created` event,
  cancels the pre-login timeout, transitions the session to `Playing`, and
  enqueues `motd` and `look`.
  (src/Tapestry.Engine/Flow/FlowEngine.cs:226-243)
- `FlowEngine.Restart(session, reason)` replaces the session's entity with a
  fresh one (via `NewPlayerEntityFactory` if set, otherwise `new Entity`),
  updates the session manager's entity-ID index, removes the old entity from
  `PlayerCreator`, and restarts the same flow from the beginning.
  (src/Tapestry.Engine/Flow/FlowEngine.cs:168-181)
- When `on_complete` returns `{success: false}` during `Creating` phase,
  `Restart` is called so the player can re-enter the flow from scratch.
  Outside `Creating`, the flow is cleared and `look` is enqueued.
  (src/Tapestry.Engine/Flow/FlowEngine.cs:120-145)
- Alignment is seeded from the sum of the class and race `StartingAlignment`
  values immediately before `on_complete` is called, but only during the
  `Creating` phase. (src/Tapestry.Engine/Flow/FlowEngine.cs:115-118, 183-195)
- `FlowEngine.GmcpSend` is an optional delegate; when set, each step renders
  a `Flow.Step` GMCP message with `type`, `prompt`, and (for choice steps)
  `options`. (src/Tapestry.Engine/Flow/FlowInstance.cs:304, 327-370)
- `FlowInstance.CommandFallback` routes `help` and `? [topic]` inputs to the
  session's command queue rather than treating them as step answers.
  (src/Tapestry.Engine/Flow/FlowInstance.cs:100-114)

### FlowRegistry

- Stores flows by ID in a case-insensitive dictionary.
- `GetByTrigger` returns all flows whose `Trigger` matches (case-insensitive);
  the engine takes the last element.
- An optional `RegistrationGate` can assert that writes occur only during the
  commit scope (the pack-seal phase), preventing runtime mutations.
  (src/Tapestry.Engine/Flow/FlowRegistry.cs:1-41)

### PlayerCreator

- Maintains an in-memory map of pending (mid-creation) entity GUIDs.
- Methods: `TrackEntity`, `GetEntity`, `Remove`, `Contains`, `All`.
- Used by `FlowEngine` to register entities when a flow starts in `Creating`
  phase and to deregister them on commit or restart.
  (src/Tapestry.Engine/Flow/PlayerCreator.cs:1-29)

### IFlowPersistence and FlowPersistenceAdapter

- `IFlowPersistence` declares two methods:
  - `PlayerExists(name) => bool` -- checks whether a save already exists.
  - `SaveNewPlayer(entity, accountId)` -- persists the new character.
  (src/Tapestry.Engine/Flow/IFlowPersistence.cs:3-7)
- `NullFlowPersistence` is the default DI registration: `PlayerExists` always
  returns `false`; `SaveNewPlayer` is a no-op. This allows `FlowEngine` to be
  resolved in test and embedded contexts without a real persistence layer.
  (src/Tapestry.Engine/Flow/IFlowPersistence.cs:9-18)
- The server registers `FlowPersistenceAdapter` as the concrete
  `IFlowPersistence`. It delegates `PlayerExists` to
  `PlayerPersistenceService.PlayerSaveExists` and `SaveNewPlayer` to
  `PlayerPersistenceService.SaveNewPlayer`. Because `SaveNewPlayer` is called
  on the connection input thread with no ambient async context,
  `.GetAwaiter().GetResult()` is used to block safely without risking a
  deadlock. (src/Tapestry.Server/Persistence/FlowPersistenceAdapter.cs:8-27)

---

## Rejected and Reverted

- None on record.

---

## Change Log

- 2026-07-22 [player-save-hygiene](changes/2026-07-22-player-save-hygiene.md) - FlowInstance gains an IFlowScratch store exposed as entity.scratch.get/set/has; step callbacks and on_complete widen to receive scratch; flows.trigger takes an optional scratch seed and the area recommend-context reads edit_area from scratch
- 2026-06-19 [pack-script-esm](changes/2026-06-19-pack-script-esm.md) - Flow registration attribution uses ScriptOwner.CurrentPackOwner/CurrentSourceFile (lexical active module) instead of __currentPack/__currentSource globals
