---
release: v0.1.38
specs: [help-system.md, command-dispatch.md, gmcp.md, output-pipeline.md]
---

# Command Catalog Display

## Why

Both command-listing surfaces - the telnet `commands` grid and the web command
palette - are meant to render the declared category vocabulary in declared order
with real labels (Trade, not `shop`; Grouping, not `group`), and to read
consistently because they read the same source. But that vocabulary lived only
in `HelpService` and `categories.yaml` and was surfaced nowhere: telnet sorted
category ids alphabetically and the web client CSS-capitalized the raw id, so
neither matched the declared order or the real labels. The display redesign that
rides on top needed the engine to expose the vocabulary to both surfaces so they
agree by construction rather than by two hand-maintained mirrors.

Rebuilding the telnet listing as a dense keyword-chip grid also surfaced a latent
rendering bug: a cell row whose fixed cells did not fill the panel width left the
right border floating, because the cell-row renderer was the only row type that
did not pad to the panel edge.

## What

- `HelpService.VisibleDeclaredCategories` exposes the declared category vocabulary
  as an ordered `(Id, Label)` list with hidden categories excluded, preserving
  declaration order within a file then pack load order across packs.
- A read-only `tapestry.commands.categories()` JS accessor returns that vocabulary
  as `[{ id, label }]` for the telnet grid to iterate in order with real labels.
- `commands.listForPlayer` rows gain an `aliases` field (surfacing the existing
  registration aliases) so a free-text command filter can match on alias.
- A new `Commands.Categories` GMCP burst pushes the same vocabulary
  (`{ categories: [{ id, label }] }`, declared order) to the client once at
  post-login, ordered immediately after the command list burst. None of these
  change the command/help model or the existing `Char.Commands` payload - they
  surface already-loaded data read-only.
- `PanelRenderer` now pads any unclaimed width on a cell row before the closing
  border, consistent with every other row type. Rows that use a fill cell already
  consume the full width, so this is a no-op for existing panels; a grid of fixed
  cells that under-fills (the new command chip grid) now aligns its right border.
