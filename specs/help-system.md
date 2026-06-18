---
capability: help-system
last-updated: 2026-06-18
---

# Help System

## Overview

Command registration carries behavior; the help topic owns all human-facing
text. A command registration declares only what the command does (keyword,
handler, roles, args, gmcp). Its title, brief, category, body, and see-also live
in a hand-authored help topic. The one piece of text the engine still derives is
the syntax line, which it stamps from the command's arg definitions at seal time.

Topics are authored in pack YAML files under `{packRoot}/help/`. `HelpSeal`
enforces cross-pack collision policy and runs three gates at boot (coverage,
category, see-also). `HelpService` handles lookup, role-visibility filtering, and
the hidden-topic listing filter. The JS surface (`tapestry.help`) exposes query,
list, and category enumeration to pack scripts. The declared-category vocabulary
that the category gate checks against is its own capability; see
[help-categories.md](help-categories.md).

---

## Behavior

### HelpTopic model

- A `HelpTopic` carries: `id`, `title`, `category`, `brief`, `body`,
  `syntax[]`, `keywords[]`, `see_also[]`, optional `role`, `override`, and
  `hidden`. (src/Tapestry.Shared/Help/HelpTopic.cs)
- Topics are namespaced as `{packName}:{id}`; both bare and namespaced ids are
  valid lookup keys. (src/Tapestry.Shared/Help/HelpTopic.cs:32; src/Tapestry.Engine/Help/HelpService.cs:199-201)
- The `role` field gates visibility: topic is visible only to entities whose
  highest role tier is >= the topic's role (hierarchy: player < builder <
  admin). A nil role is visible to all including pre-login (chargen).
  (src/Tapestry.Engine/Help/HelpService.cs:297-316)

### Registration (HelpSeal)

- Packs supply help files as `*.yaml` under `{packRoot}/help/`. Files are
  loaded via `HelpService.LoadPack` and routed through `RegistrationPolicy`
  (Kind "help") so cross-pack same-id collisions produce boot errors unless
  one side declares `{ override: true }` with a dependency edge.
  (src/Tapestry.Engine/Help/HelpService.cs:80-184)
- After `RegistrationPolicy.Resolve()`, `HelpSeal.Seal()` runs two passes and
  then three gates. Pass 1 is command-shadowing authority: a hand-authored
  topic whose id matches a resolved command owned by a different pack must
  declare `override: true` and a dependency edge. Non-pack command owners are
  exempt -- empty, `kernel`, `engine`, and `core` -- so a pack (typically
  `@tapestry/core`) may document a built-in command. Pass 2 stamps the
  args-derived syntax line onto authored command topics.
  (src/Tapestry.Engine/Help/HelpSeal.cs:50-97)

### Syntax stamping (CommandHelpGenerator)

- The old silent gap-fill is gone. `CommandHelpGenerator` no longer fabricates a
  topic for every command with arg definitions; a command with no authored topic
  is a coverage violation, not a junk topic. `StampSyntax` instead walks the
  registrations and, for each one that already has a matching authored topic,
  overwrites that topic's `syntax` with the args-derived line. Concept topics
  (no matching command) are untouched. (src/Tapestry.Engine/CommandHelpGenerator.cs:12-20)
- `BuildSyntax` is the kept, null-safe builder. Required args render as `[arg]`,
  optional as `([arg])`, bulk as `[arg | all | all.arg]`. Prepositions are
  inserted before the placeholder. (src/Tapestry.Engine/CommandHelpGenerator.cs:22-44)

### Help registry gates (HelpSeal)

- Three gates run at the post-merge point and aggregate all violations into one
  report. Coverage: every registered command needs an authored topic whose id
  matches the keyword. Category: every authored topic's category must be a
  declared category id, with a Levenshtein nearest-match did-you-mean (the
  category vocabulary is owned by [help-categories.md](help-categories.md)).
  See-also: every `see_also` id must resolve in the merged topic set (loose).
  (src/Tapestry.Engine/Help/HelpSeal.cs:98-146)
- Strictness is scoped to these three gates only. `HelpSealOptions.Strict`
  defaults to true and is sourced from the `TAPESTRY_HELP_GATES` env var;
  `lenient` flips it. Strict throws the aggregated list and fails boot; lenient
  logs each violation as a warning and continues. The pre-existing Pass-1 shadow
  gate and `RegistrationPolicy` stay always-strict regardless of this flag.
  (src/Tapestry.Engine/Help/HelpSealOptions.cs:9-12; src/Tapestry.Engine/Help/HelpSeal.cs:151-163; src/Tapestry.Server/Program.cs:85-89)

### Lookup (HelpService)

- `Query` resolves in order: exact id match -> exact title match ->
  fuzzy (title/keyword substring). A single fuzzy match returns `"ok"`;
  multiple fuzzy matches return `"multiple"` with a summary list; no match
  returns `"no_match"`. (src/Tapestry.Engine/Help/HelpService.cs:209-242)
- Lookup is case-insensitive throughout. (tests/Tapestry.Engine.Tests/Help/HelpServiceTests.cs:54-59)
- Load-order no longer breaks ties; the last `AddTopic` call for a given id
  wins (RegistrationPolicy is the cross-pack authority).
  (src/Tapestry.Engine/Help/HelpService.cs:277-284; tests/Tapestry.Engine.Tests/Help/HelpServiceTests.cs:62-77; commit 90c4eb4)
- `List(entityId, category)` and `Categories(entityId)` apply the same
  role-visibility filter as `Query`. (src/Tapestry.Engine/Help/HelpService.cs:252-272)

### Hidden listing filter (HelpService)

- A topic carries its own `hidden` flag in addition to its category's hidden
  flag. Effective listing requires both off: the category is NOT hidden AND the
  topic is NOT hidden. `IsListed` is the shared predicate.
  (src/Tapestry.Shared/Help/HelpTopic.cs:25-26; src/Tapestry.Engine/Help/HelpService.cs:244-250)
- `List` and `Categories` filter hidden topics and hidden categories out of the
  catalog. `Query` and `GetTopicById` stay unfiltered, so `help <name>` and
  dispatch reach a hidden topic directly.
  (src/Tapestry.Engine/Help/HelpService.cs:252-272; src/Tapestry.Engine/Help/HelpService.cs:186-190)

### Read-path repoint (command catalog)

- The command catalog reads category and brief from the help topic, not from the
  registration. `CommandsModule.ListForPlayer` and `CharCommandsHandler` look up
  the topic by keyword, apply `IsListed`, and pull `topic.Category` and
  `topic.Brief`. The old `DeriveCategory` helpers, the hardcoded
  `KeywordCategoryOverrides`, and `NormalizeCategory` are deleted.
  (src/Tapestry.Scripting/Modules/CommandsModule.cs:297-301; src/Tapestry.Server/Gmcp/Handlers/CharCommandsHandler.cs:57-65)
- `HelpService.VisibleDeclaredCategories` projects the declared category vocabulary as an
  ordered `(Id, Label)` list with hidden categories excluded, preserving declaration order
  within a file then pack load order across packs (the vocabulary itself is owned by
  [help-categories.md](help-categories.md)). The command catalog reads it so both the telnet
  grid and the `Commands.Categories` GMCP burst render category sections in declared order
  with real labels instead of re-deriving order or labels per surface.
  (src/Tapestry.Engine/Help/HelpService.cs:58-62)

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

- 2026-06-18 [command-catalog-display](changes/2026-06-18-command-catalog-display.md)
- 2026-06-17 [command-help-registry](changes/2026-06-17-command-help-registry.md)

---

## Related capabilities

- HelpRenderer terminal formatting details (CRLF, 78-column width) are
  referenced here as a seam; the rendering stack is owned by output-pipeline.md.
