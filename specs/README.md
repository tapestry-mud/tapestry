# Tapestry Engine -- specs

Capability specs for the Tapestry engine. Each file describes one system's current behavior,
known constraints, and change history.

## Index

| Capability | File | Last Updated |
|------------|------|--------------|
| Command Dispatch | [command-dispatch.md](command-dispatch.md) | 2026-06-12 |
| Registries and Seal | [registries-and-seal.md](registries-and-seal.md) | 2026-06-12 |
| Pack Security | [pack-security.md](pack-security.md) | 2026-06-12 |
| Area Authoring | [area-authoring.md](area-authoring.md) | 2026-06-12 |

## Contract summary

Each capability spec has four required sections: Overview, Behavior, Rejected and Reverted,
Change Log. Change records live in `specs/changes/` and use the frontmatter fields `release:`
(engine or pack version that shipped it) and `repos:` (capability files touched).

Hotfixes, regressions, and dependency bumps owe no change record. Tombstones on any reversal
of shipped behavior are mandatory.

A capability spec is current if its Change Log references the latest shipped change record
that names it in `repos:` (Rule 4).

## Note on docs/ vs specs/

`docs/` is gitignored -- internal design notes stay out of this repo. `specs/` is a
deliberate public surface with different rules: it is the canonical source of truth for
how each engine system behaves now.

## Pending

When `@tapestry/packs` (socials pack) first participates in a spec's `repos:` block, plant
the ROM socials tombstone in that repo's `specs/` under the relevant capability:
the ~20 unshipped socials are deliberate omissions (offensive by modern standards), not a
gap to fill.
