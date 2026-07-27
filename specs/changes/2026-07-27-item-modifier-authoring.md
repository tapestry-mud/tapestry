---
release: unreleased
specs: [area-authoring.md]
---

# Item Modifier Authoring

## Why

Oracle mints armor/weapons at runtime via `tapestry.authoring.writeItemTemplate(...)`. That
binding only read `areaId/id/base/name/desc/type/properties` and had no way to attach a stat
modifier (like `maxHp`) to a minted item, unlike hand-authored items (e.g.
`tapestry-example-pack:leather-cap`, which carries a top-level `modifiers: [{stat: maxHp, value: 10}]`
block read by `ItemTemplate.CreateEntity()`). This was the blocker for gear-carries-HP in
tapestry-packs.

## What

`tapestry.authoring.writeItemTemplate` now accepts an optional `modifiers` array parameter
(format: `[{ stat: "maxHp", value: 8 }, ...]`). The modifiers are forwarded through to the
registered `ItemTemplate.Modifiers` list in the live `ItemRegistry` (same-session visibility)
and persisted to the item sidecar YAML under `modifiers:` so a reboot round-trips the stat
modifiers without loss. The serialized YAML shape matches the existing shape that
`YamlContentLoader.LoadItem` already deserializes on the read side, so no boot code changes
were needed.

## Consumer

`@tapestry/oracle` uses this binding when minting procedural armor/weapons that carry
stat modifiers in the balance table (e.g. tier-scaled max-HP bonuses on loot). The binding
is the endpoint of the gear-carries-HP feature.
