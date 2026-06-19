This directory contains the canonical TypeScript declaration file for the `@tapestry/engine`
module - the type contract that ESM packs compile against. `tapestry-engine.d.ts` is the
source of truth; the `tapestry-cli` vendors a copy via the `tapestry types` command (Phase B)
and lays it into each pack's `types/` directory so pack authors get IntelliSense and type
checking without managing this file themselves. Keep it in sync with the `IJintApiModule` set
in `src/Tapestry.Scripting/Modules/` and tag it to the engine version in the commit message
when updating. The declaration covers the called surface enumerated from live packs and grows
incrementally as packs migrate to ESM; the index-signature safety net on each namespace keeps
that migration mechanical.
