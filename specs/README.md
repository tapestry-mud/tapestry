# Tapestry Engine -- specs

Capability specs for the Tapestry engine. Each file describes one system's current behavior,
known constraints, and change history.

## Index

| Capability | File | Last Updated |
|------------|------|--------------|
| Command Dispatch | [command-dispatch.md](command-dispatch.md) | 2026-06-21 |
| Registries and Seal | [registries-and-seal.md](registries-and-seal.md) | 2026-07-22 |
| Pack Security | [pack-security.md](pack-security.md) | 2026-07-04 |
| Pack Loading | [pack-loading.md](pack-loading.md) | 2026-07-04 |
| Scripting Runtime | [scripting-runtime.md](scripting-runtime.md) | 2026-07-04 |
| Area Authoring | [area-authoring.md](area-authoring.md) | 2026-07-22 |
| GMCP | [gmcp.md](gmcp.md) | 2026-07-04 |
| Heartbeat | [heartbeat.md](heartbeat.md) | 2026-06-22 |
| Mob AI | [mob-ai.md](mob-ai.md) | 2026-07-03 |
| Mob Lifecycle | [mob-lifecycle.md](mob-lifecycle.md) | 2026-07-03 |
| Combat Resolution | [combat-resolution.md](combat-resolution.md) | 2026-07-22 |
| Death and Corpses | [death-and-corpses.md](death-and-corpses.md) | 2026-07-03 |
| Output Pipeline | [output-pipeline.md](output-pipeline.md) | 2026-07-23 |
| Rest and Recovery | [rest-and-recovery.md](rest-and-recovery.md) | 2026-07-04 |
| Persistence | [persistence.md](persistence.md) | 2026-07-22 |
| Telnet Protocol | [telnet-protocol.md](telnet-protocol.md) | 2026-06-14 |
| Sessions and Connections | [sessions-and-connections.md](sessions-and-connections.md) | 2026-06-13 |
| Login and Accounts | [login-and-accounts.md](login-and-accounts.md) | 2026-06-13 |
| Flows and Wizards | [flows-and-wizards.md](flows-and-wizards.md) | 2026-07-22 |
| Events | [events.md](events.md) | 2026-07-04 |
| World Entity Store | [world-entity-store.md](world-entity-store.md) | 2026-07-22 |
| World Geography | [world-geography.md](world-geography.md) | 2026-06-27 |
| Items and Containers | [items-and-containers.md](items-and-containers.md) | 2026-07-03 |
| Equipment and Modifiers | [equipment-and-modifiers.md](equipment-and-modifiers.md) | 2026-06-12 |
| Quests | [quests.md](quests.md) | 2026-06-12 |
| Abilities | [abilities.md](abilities.md) | 2026-07-04 |
| Effects and Modifiers | [effects-and-modifiers.md](effects-and-modifiers.md) | 2026-06-12 |
| Character Progression | [character-progression.md](character-progression.md) | 2026-06-12 |
| Alignment | [alignment.md](alignment.md) | 2026-06-12 |
| Economy and Shops | [economy-and-shops.md](economy-and-shops.md) | 2026-07-03 |
| World Simulation | [world-simulation.md](world-simulation.md) | 2026-07-03 |
| Admin Commands | [admin-commands.md](admin-commands.md) | 2026-06-12 |
| Help System | [help-system.md](help-system.md) | 2026-06-18 |
| Telemetry | [telemetry.md](telemetry.md) | 2026-06-12 |
| Help Categories | [help-categories.md](help-categories.md) | 2026-06-17 |

## Contract summary

Each capability spec has four required sections: Overview, Behavior, Rejected and Reverted,
Change Log. Change records live in `specs/changes/` and use the frontmatter fields `release:`
(engine or pack version that shipped it) and `specs:` (capability files touched).

Hotfixes, regressions, and dependency bumps owe no change record. Tombstones on any reversal
of shipped behavior are mandatory.

A capability spec is current if its Change Log references the latest shipped change record
that names it in `specs:` (Rule 4).

Format rules (mechanically linted):

- Behavior claims carry inline anchors in exactly one form: `(repo-relative/path/File.ext:123)`,
  where the line part may be a single line `:123` or a range `:123-145`, and may be omitted only
  for whole-file claims. Several anchors may share one set of parentheses, joined by `; `. A test
  name in the same parentheses also counts. Lint pattern (the gate IS this regex, keep them in
  sync): `\([@\w./\\-]+\.(cs|js|ts|json|ya?ml)(:\d+(-\d+)?)?[^)]*\)`. A file with no matches in its
  Behavior section fails validation outright.
- An empty Rejected and Reverted section contains the single line `- None on record.` under
  the heading (the heading itself is always present).
- Change Log is a one-line-per-record list, newest first: `- YYYY-MM-DD [slug](changes/...)`.
  Not a table.

## Note on docs/ vs specs/

`docs/` is gitignored -- internal design notes stay out of this repo. `specs/` is a
deliberate public surface with different rules: it is the canonical source of truth for
how each engine system behaves now.

<!-- spec-lint:start -->
Mode: strict

Required sections: Overview, Behavior, Rejected and Reverted, Change Log

Anchor regex (Behavior): \([@\w./\\-]+\.(cs|js|ts|json|ya?ml|md)(:\d+(-\d+)?)?[^)]*\)

Empty-reversal sentinel: - None on record.

Change Log: list, newest-first by date, not a table. Empty is valid for unmodified capabilities.

Index sync: every capability .md on disk appears in README index; every indexed file exists on disk; index date matches file last-updated.

Currency: for each change record naming a capability, the top Change Log entry references that record and last-updated >= record date. A capability named by zero records may have an empty Change Log.

Tombstone: a change record with status:reverted requires a tombstone entry in the capability Rejected and Reverted (not the empty sentinel).
<!-- spec-lint:end -->
