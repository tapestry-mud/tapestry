---
release: v0.1.37
specs: [help-system.md, command-dispatch.md, help-categories.md]
---

# Command Help Registry

## Why

Command metadata and help metadata had drifted into two homes. A command
registration carried its own `description` and `category`, and the help topic
carried the same fields again. The two could disagree, and the command catalog
read one while `help <name>` read the other. On top of that, the help seal would
silently fabricate a topic for any command that had arg definitions but no
authored help, so a command with no real documentation still showed up as a
"covered" topic full of placeholder text.

This change collapses the two homes into one. The command registration keeps
behavior; the help topic owns every piece of human-facing text. The engine stops
fabricating topics and instead fails the boot when a command has no authored
help, so a missing topic is loud, not papered over. Categories become a declared
vocabulary so a typo cannot quietly create a new category.

## What

- `CommandRegistration` loses `Description` and `Category`, and `Register()`
  drops both parameters. A registration now carries keyword, handler, aliases,
  priority, roles, arg definitions, gmcp config, and visibility only.
  (src/Tapestry.Engine/CommandRegistry.cs:6-24; src/Tapestry.Engine/CommandRegistry.cs:37-47)

- `HelpSeal.Seal()` runs three new gates at the post-merge point and aggregates
  every violation into one throw (strict) or one warn-list (lenient). Coverage:
  every registered command needs an authored topic whose id matches the keyword.
  Category: every authored topic's category must be a declared category id, with
  a Levenshtein nearest-match did-you-mean. See-also: every `see_also` id must
  resolve in the merged topic set (loose).
  (src/Tapestry.Engine/Help/HelpSeal.cs:98-146)

- `HelpSealOptions.Strict` defaults to true and is sourced from the
  `TAPESTRY_HELP_GATES` env var (`lenient` flips it). The flag is scoped only to
  the three new gates; the pre-existing Pass-1 shadow gate and
  `RegistrationPolicy` stay always-strict.
  (src/Tapestry.Engine/Help/HelpSealOptions.cs:9-12; src/Tapestry.Engine/Help/HelpSeal.cs:151-163; src/Tapestry.Server/Program.cs:85-89)

- The silent gap-fill is deleted. `CommandHelpGenerator.GenerateGaps` and
  `GenerateFor` are gone, so the engine no longer fabricates topics from arg
  definitions. `BuildSyntax` is kept (public, null-safe), and a new `StampSyntax`
  writes the args-derived syntax line onto authored command topics only, at seal
  time. (src/Tapestry.Engine/CommandHelpGenerator.cs:12-44)

- Categories become a declared vocabulary. `CategoryDeclaration` and
  `CategoriesFile` model a `help/categories.yaml` sequence (a list; order is
  load-bearing). `HelpService` gains `LoadCategories`, `RegisterCategory`,
  `DeclaredCategoryIds`, and `IsCategoryHidden`. Ordering is declaration order
  within a file, then pack load order across packs (append-at-end; relative
  anchoring deferred). A category may declare `hidden: true`.
  (src/Tapestry.Engine/Help/CategoryDeclaration.cs:1-13; src/Tapestry.Engine/Help/HelpService.cs:51-68; src/Tapestry.Engine/Help/HelpService.cs:115-156)

- `HelpTopic` gains a topic-level `hidden` field. Effective listing is category
  NOT hidden AND topic NOT hidden, expressed by the shared `IsListed` predicate.
  `List` and `Categories` filter hidden out; `Query` and `GetTopicById` stay
  unfiltered, so direct `help <name>` and dispatch still reach a hidden topic.
  (src/Tapestry.Shared/Help/HelpTopic.cs:25-26; src/Tapestry.Engine/Help/HelpService.cs:244-272)

- The command catalog read path repoints to the help topic.
  `CommandsModule.ListForPlayer` and `CharCommandsHandler` read category and
  brief from the topic and apply `IsListed`; the old `DeriveCategory` helpers,
  the hardcoded `KeywordCategoryOverrides`, and `NormalizeCategory` are deleted.
  (src/Tapestry.Scripting/Modules/CommandsModule.cs:297-301; src/Tapestry.Server/Gmcp/Handlers/CharCommandsHandler.cs:57-65)

- The Pass-1 shadow gate exempts the bare owner `core` alongside `kernel` and
  `engine`. C# Server modules register built-in commands (for example
  resetpassword and save) under the bare owner `core`, distinct from the
  `@tapestry/core` pack namespace, so the core pack can author help topics for
  those commands without tripping the shadow gate.
  (src/Tapestry.Engine/Help/HelpSeal.cs:70-78)
