---
release: 0.1.41
specs: [scripting-runtime.md, pack-loading.md]
---

# Solo Oracle V2 Seams

## Why

The solo oracle v2 design introduced six engine seams (T1-T6) that the oracle pack
needs to generate and read frozen table data. The v1 seams change record covered the
spawn/movement layer (E1-E4); these seams cover the oracle data layer.

## What

- **T1: OracleTableRegistry** -- new registry (`IOracleTableRegistry`) stores frozen
  `OracleTableData` keyed by `areaId:kind`; registered as DI singleton; sealed with the
  other registries. (pack-loading.md)

- **T2: Pack content kind `oracle`** -- `PackLoader` routes the `oracle:` glob pattern to
  `LoadOracleData`, which parses `*-oracle-table.yaml` side-cars and registers them in
  `OracleTableRegistry` at boot. (pack-loading.md)

- **T3: `tapestry.oracle.table(id)` read binding** -- `OracleModule` (`IJintApiModule`)
  exposes a read-only lookup over the registry. Pack JS rolls entirely from the returned
  entries; no dice cross the scripting boundary. (scripting-runtime.md)

- **T6: `AuthoredOracleLoader` boot scanner** -- loads `*-oracle-table.yaml` files from
  the authoring root at boot/reload, so frozen tables written by `writeOracleTable` survive
  restarts without re-generation. (pack-loading.md)
