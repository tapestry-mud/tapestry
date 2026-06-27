---
release: 0.1.43
specs: [scripting-runtime.md, world-geography.md, area-authoring.md]
---

# Oracle Six Axis Overlay

## Why

Generated solo areas need to change and remember what happens in them - a room that
gets looted, a boss that gets slain, an exit that collapses - without those mutations
ever leaking into a shared pack or a harvested side-car. The mutations must reset on
reboot (the minted world reloads from its side-cars; the deltas evaporate) so the
"connection exits never persist" invariant holds by construction.

A second problem surfaced once a runtime-generated pack ran in the containerized
deployment: `createPack` wrote its destination-pack scaffold into the packs directory,
which the engine process (a non-root uid) cannot write when packs is a deploy-owned
bind mount. The write threw and crashed the generation flow. The namespace registration
that the scaffold provided had to move to a location the engine actually owns.

## What

- **In-memory consequence overlay + `tapestry.consequence` binding** (scripting-runtime.md).
  A memory-only store keyed `(roomId, kind)` with an opaque `kind` and `lifespan`; the
  engine never reasons about content meaning. `stamp`/`list`/`has`/`clear` plus eviction
  routed by lifespan: ephemeral entries clear on the area repop tick (`area.tick`);
  persistent and succession-seed survive until reboot. The store resets on restart by
  construction - no disk state - so consequences can never leak into a harvest.

- **Runtime-only `collapseExit`** (world-geography.md). Removes a directional exit from the
  live world graph in memory only (calls `Room.RemoveExit`, never writes a side-car), and
  records the collapse under the caller-supplied opaque kind/lifespan. On reboot the
  side-car reload restores the exit and the overlay is empty, so the mutation cleanly
  evaporates. The kind/lifespan are parameters - the engine bakes in no content string.

- **Docker-safe runtime namespace persistence** (area-authoring.md). `createPack` now
  registers a runtime-created namespace via a new `RuntimeNamespaceStore`, which persists
  it to a `runtime-namespaces.txt` marker under the writable data directory and
  re-registers it at boot (so post-reboot lazy-mint into that namespace still resolves).
  The packs-directory `pack.yaml` scaffold write is now best-effort (try/catch): an
  unwritable packs directory (the non-root engine uid in the container) no longer crashes
  the call. The generated content already persists as data side-cars loaded independently
  of the scaffold, so the marker is the only thing needed for the namespace to survive.

Shipped across `0.1.42` (consequence overlay + `collapseExit`) and `0.1.43` (docker-safe
namespace persistence). `0.1.42` also carried the previously-merged solo-oracle area
generation seams as the first tagged release that includes them.
