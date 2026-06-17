---
capability: help-categories
last-updated: 2026-06-17
---

# Help Categories

## Overview

Help topics group under a category. A category is no longer a free-form string a
topic invents; it must be a declared id from a vocabulary a pack publishes. The
vocabulary is an ordered list loaded from a pack's `help/categories.yaml`. A
category may be marked hidden to keep its topics out of listing surfaces while
leaving them directly help-able. The category gate enforces the vocabulary at
seal time and offers a did-you-mean hint when a topic names something close but
wrong.

## Behavior

### Declared category vocabulary

- A category declaration carries `id`, `label`, and an optional `hidden` flag.
  The on-disk file is a YAML mapping with a top-level `categories:` sequence;
  order within the sequence is load-bearing. (src/Tapestry.Engine/Help/CategoryDeclaration.cs:3-13)
- Categories load from a pack's `help/categories.yaml`. `LoadCategories` reads
  the file if present, deserializes it as a `CategoriesFile`, and registers each
  declaration; a missing file is a no-op and a malformed file is logged and
  skipped, not fatal. (src/Tapestry.Engine/Help/HelpService.cs:115-156)
- Registration is recorded through the seal's `RegistrationPolicy` under Kind
  `help-category` when a policy is present, so categories commit alongside the
  rest of the registry; in direct unit construction (no policy) it registers
  eagerly. (src/Tapestry.Engine/Help/HelpService.cs:142-155)

### Ordering

- The declared id list is ordered by load order, append-at-end: declaration
  order within a file first, then pack load order across packs. A later pack's
  categories append after an earlier pack's; relative anchoring (inserting one
  category before or after another) is not supported.
  (src/Tapestry.Engine/Help/HelpService.cs:54-55; src/Tapestry.Engine/Help/HelpService.cs:57-61)
- `DeclaredCategoryIds` is the ordered projection consumers read; it sorts the
  recorded declarations by their captured load order and yields ids in that
  order. (src/Tapestry.Engine/Help/HelpService.cs:53-55)

### Hidden categories

- A declaration with `hidden: true` marks the whole category hidden.
  `IsCategoryHidden` reports it. (src/Tapestry.Engine/Help/HelpService.cs:63-68)
- A hidden category drops out of listing surfaces: `List` returns empty for it
  and `Categories` omits it. The category's topics stay reachable by direct
  lookup. (src/Tapestry.Engine/Help/HelpService.cs:252-272)

### Category gate (seal)

- At seal time, every authored topic's category must be a declared category id.
  A topic with a blank or unknown category is a violation.
  (src/Tapestry.Engine/Help/HelpSeal.cs:110-129)
- The gate is skipped entirely when the declared vocabulary is empty. An empty
  vocabulary means the category system has not been seeded yet, so the gate
  stays dormant during migration rather than failing every topic.
  (src/Tapestry.Engine/Help/HelpSeal.cs:113-115)
- An unknown category produces a did-you-mean hint: the nearest declared id by
  Levenshtein distance is suggested, and the full declared list is printed.
  (src/Tapestry.Engine/Help/HelpSeal.cs:122-127; src/Tapestry.Engine/Help/HelpSeal.cs:165-194)
- The gate aggregates with the coverage and see-also gates into one report.
  Under strict mode the combined list throws and fails boot; under lenient mode
  each violation logs as a warning and boot continues.
  (src/Tapestry.Engine/Help/HelpSeal.cs:148-163)

## Rejected and Reverted

- None on record.

## Change Log

- 2026-06-17 [command-help-registry](changes/2026-06-17-command-help-registry.md)
