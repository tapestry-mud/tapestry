---
capability: help-system
last-updated: 2026-06-12
---

# Help System

## Overview

The help system provides a model for topic registration, command-derived topic
generation, and lookup. Topics are authored in pack YAML files or auto-generated
from command `ArgDefinitions`. `HelpSeal` enforces cross-pack collision policy
at boot. `HelpService` handles lookup and role-visibility filtering. The JS
surface (`tapestry.help`) exposes query, list, and category enumeration to pack
scripts.

---

## Behavior

### HelpTopic model

- A `HelpTopic` carries: `id`, `title`, `category`, `brief`, `body`,
  `syntax[]`, `keywords[]`, `see_also[]`, optional `role`, and `override`.
  (src/Tapestry.Shared/Help/HelpTopic.cs)
- Topics are namespaced as `{packName}:{id}`; both bare and namespaced ids are
  valid lookup keys. (src/Tapestry.Shared/Help/HelpTopic.cs:28; src/Tapestry.Engine/Help/HelpService.cs:125-126)
- The `role` field gates visibility: topic is visible only to entities whose
  highest role tier is >= the topic's role (hierarchy: player < builder <
  admin). A nil role is visible to all including pre-login (chargen).
  (src/Tapestry.Engine/Help/HelpService.cs:207-215)

### Registration (HelpSeal)

- Packs supply help files as `*.yaml` under `{packRoot}/help/`. Files are
  loaded via `HelpService.LoadPack` and routed through `RegistrationPolicy`
  (Kind "help") so cross-pack same-id collisions produce boot errors unless
  one side declares `{ override: true }` with a dependency edge.
  (src/Tapestry.Engine/Help/HelpService.cs:60-121)
- After `RegistrationPolicy.Resolve()`, `HelpSeal.Seal()` runs two passes:
  (1) command-shadowing authority -- a hand-authored topic whose id matches
  a resolved command owned by a different pack must declare `override: true`
  and a dependency edge; engine/kernel-owned commands are exempt;
  (2) auto-gen gap-fill -- `CommandHelpGenerator.GenerateGaps` generates a
  topic for every resolved command that has `ArgDefinitions` and no
  winning hand-authored topic.
  (src/Tapestry.Engine/Help/HelpSeal.cs:36-82)

### Command-derived help generation (CommandHelpGenerator)

- `CommandHelpGenerator.GenerateFor` returns null if a registration has no
  `ArgDefinitions`; otherwise it builds a topic with a syntax line from the
  arg definitions. (src/Tapestry.Engine/CommandHelpGenerator.cs:8-28)
- Syntax format: required args render as `[arg]`, optional as `([arg])`, bulk
  as `[arg | all | all.arg]`. Prepositions are inserted before the placeholder.
  (src/Tapestry.Engine/CommandHelpGenerator.cs:51-69; tests/Tapestry.Engine.Tests/CommandHelpGeneratorTests.cs)

### Lookup (HelpService)

- `Query` resolves in order: exact id match -> exact title match ->
  fuzzy (title/keyword substring). A single fuzzy match returns `"ok"`;
  multiple fuzzy matches return `"multiple"` with a summary list; no match
  returns `"no_match"`. (src/Tapestry.Engine/Help/HelpService.cs:135-167)
- Lookup is case-insensitive throughout. (tests/Tapestry.Engine.Tests/Help/HelpServiceTests.cs:54-59)
- Load-order no longer breaks ties; the last `AddTopic` call for a given id
  wins (RegistrationPolicy is the cross-pack authority).
  (src/Tapestry.Engine/Help/HelpService.cs:192-201; tests/Tapestry.Engine.Tests/Help/HelpServiceTests.cs:62-77; commit 90c4eb4)
- `List(entityId, category)` and `Categories(entityId)` apply the same
  role-visibility filter as `Query`. (src/Tapestry.Engine/Help/HelpService.cs:170-188)

### Pack JS surface (HelpModule)

- `tapestry.help.query(entityId?, term)` -> `{status, topic?|matches?|term?}`
- `tapestry.help.list(entityId?, category)` -> `[{id, title, brief}]`
- `tapestry.help.categories(entityId?)` -> `[string]`
  (src/Tapestry.Scripting/Modules/HelpModule.cs)
- When the first argument is a GUID it is treated as the player context and
  the second argument is the term; otherwise the first argument is the term
  and the context is anonymous. (src/Tapestry.Scripting/Modules/HelpModule.cs:111-123)

---

## Rejected and Reverted

- None on record.

---

## Change Log

---

## Related capabilities

- HelpRenderer terminal formatting details (CRLF, 78-column width) are
  referenced here as a seam; the rendering stack is owned by output-pipeline.md.
