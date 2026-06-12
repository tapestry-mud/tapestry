# Tapestry Engine -- specs

Capability specs for the Tapestry engine. Each file describes one system's current behavior,
known constraints, and change history.

## Index

| Capability | File | Last Updated |
|------------|------|--------------|
| Command Dispatch | [command-dispatch.md](command-dispatch.md) | 2026-06-12 |
| Registries and Seal | [registries-and-seal.md](registries-and-seal.md) | 2026-06-12 |
| Pack Security | [pack-security.md](pack-security.md) | 2026-06-12 |
| Pack Loading | [pack-loading.md](pack-loading.md) | 2026-06-12 |
| Scripting Runtime | [scripting-runtime.md](scripting-runtime.md) | 2026-06-12 |
| Area Authoring | [area-authoring.md](area-authoring.md) | 2026-06-12 |
| GMCP | [gmcp.md](gmcp.md) | 2026-06-12 |
| Heartbeat | [heartbeat.md](heartbeat.md) | 2026-06-12 |
| Mob AI | [mob-ai.md](mob-ai.md) | 2026-06-12 |
| Mob Lifecycle | [mob-lifecycle.md](mob-lifecycle.md) | 2026-06-12 |
| Combat Resolution | [combat-resolution.md](combat-resolution.md) | 2026-06-12 |
| Death and Corpses | [death-and-corpses.md](death-and-corpses.md) | 2026-06-12 |
| Output Pipeline | [output-pipeline.md](output-pipeline.md) | 2026-06-12 |
| Rest and Recovery | [rest-and-recovery.md](rest-and-recovery.md) | 2026-06-12 |
| Persistence | [persistence.md](persistence.md) | 2026-06-12 |
| Telnet Protocol | [telnet-protocol.md](telnet-protocol.md) | 2026-06-12 |
| Sessions and Connections | [sessions-and-connections.md](sessions-and-connections.md) | 2026-06-12 |
| Login and Accounts | [login-and-accounts.md](login-and-accounts.md) | 2026-06-12 |
| Flows and Wizards | [flows-and-wizards.md](flows-and-wizards.md) | 2026-06-12 |
| Events | [events.md](events.md) | 2026-06-12 |
| World Entity Store | [world-entity-store.md](world-entity-store.md) | 2026-06-12 |
| World Geography | [world-geography.md](world-geography.md) | 2026-06-12 |
| Items and Containers | [items-and-containers.md](items-and-containers.md) | 2026-06-12 |
| Equipment and Modifiers | [equipment-and-modifiers.md](equipment-and-modifiers.md) | 2026-06-12 |
| Quests | [quests.md](quests.md) | 2026-06-12 |
| Abilities | [abilities.md](abilities.md) | 2026-06-12 |
| Effects and Modifiers | [effects-and-modifiers.md](effects-and-modifiers.md) | 2026-06-12 |
| Character Progression | [character-progression.md](character-progression.md) | 2026-06-12 |
| Alignment | [alignment.md](alignment.md) | 2026-06-12 |
| Economy and Shops | [economy-and-shops.md](economy-and-shops.md) | 2026-06-12 |
| World Simulation | [world-simulation.md](world-simulation.md) | 2026-06-12 |
| Admin Commands | [admin-commands.md](admin-commands.md) | 2026-06-12 |
| Help System | [help-system.md](help-system.md) | 2026-06-12 |
| Telemetry | [telemetry.md](telemetry.md) | 2026-06-12 |

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
