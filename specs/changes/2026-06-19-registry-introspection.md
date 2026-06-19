---
release: v0.1.39
specs: [registries-and-seal.md, scripting-runtime.md]
---

# Registry Introspection

## Why

The registration policy already holds a complete provenance ledger for the
nineteen registry kinds that route through the seal - every registration's kind,
name, owner, source file and line, override flag, and the resolved winner per
`(kind, name)`. None of it was readable at runtime. When a command, ability, or
hook fired the wrong version or silently went dead, there was no way to ask the
running engine who won a name and what it shadowed; that meant a forensic dig
across packs even though the answer was already in the ledger.

Two registries made the gap visible: tags had a registry browser but properties
had none, even though properties carry richer metadata (value type, range, enum,
applies-to). The data existed; nothing surfaced it.

## What

- `RegistrationPolicy` gains two read-only methods, safe to call post-seal:
  `GetRegistrySummary()` returns one row per kind with its name count and
  shadow-conflict count; `GetRegistrations(kind, name?)` returns the full ledger
  for a kind (optionally one name), each entry marked winner/override and carrying
  the shadow relationship (an override winner names the base it shadows; a shadowed
  loser names the winner). Two flat records back them - `RegistrySummaryRow` and
  `RegistryEntryView` - deliberately primitive-only so a later transport can
  serialize them without a rewrite.
- A new `RegistryModule` (`IJintApiModule`) exposes the read surface to pack JS as
  `tapestry.registry.summary()`, `tapestry.registry.list(kind, name?)`, and
  `tapestry.registry.conflicts()`. The module adapts the two namespaced outliers
  (property, tag) in at the interop layer rather than inside `RegistrationPolicy`,
  normalizing their `GetAll()` data into the same shape the policy kinds return so
  a renderer does not branch. Summary rows tag each kind `policy` or `namespaced`
  so a consumer knows which vocabulary applies - shadow/override for policy kinds,
  ambiguity (a bare name declared by two or more packs) for the namespaced two.
- The change is purely additive: read methods plus a new interop module, no change
  to the registration path, the seal, or the resolution model. Strict boot is
  unaffected. One engine unit test asserts `GetRegistrations` returns the winner
  plus the shadowed loser for a deliberately collided fixture and that
  `GetRegistrySummary` counts conflicts correctly.
