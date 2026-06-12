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
| GMCP | [gmcp.md](gmcp.md) | 2026-06-12 |
| Heartbeat | [heartbeat.md](heartbeat.md) | 2026-06-12 |
| Mob AI | [mob-ai.md](mob-ai.md) | 2026-06-12 |
| Combat | [combat.md](combat.md) | 2026-06-12 |
| Output Pipeline | [output-pipeline.md](output-pipeline.md) | 2026-06-12 |
| Persistence | [persistence.md](persistence.md) | 2026-06-12 |

## Contract summary

Each capability spec has four required sections: Overview, Behavior, Rejected and Reverted,
Change Log. Change records live in `specs/changes/` and use the frontmatter fields `release:`
(engine or pack version that shipped it) and `specs:` (capability files touched).

Hotfixes, regressions, and dependency bumps owe no change record. Tombstones on any reversal
of shipped behavior are mandatory.

A capability spec is current if its Change Log references the latest shipped change record
that names it in `specs:` (Rule 4).

## Note on docs/ vs specs/

`docs/` is gitignored -- internal design notes stay out of this repo. `specs/` is a
deliberate public surface with different rules: it is the canonical source of truth for
how each engine system behaves now.
