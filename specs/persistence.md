---
capability: persistence
last-updated: 2026-06-13
---

# Persistence

## Overview

Tapestry persists two independent scopes: accounts and player characters. Both are
serialized to YAML and written to a configurable on-disk directory tree. The engine
assembly defines interfaces and DTOs; the server assembly supplies the file-backed
implementations. Runtime mob state (current HP, position, inventory acquired during
play) is not persisted across restarts. Authored rooms and areas do persist via
side-car YAML (WorldAuthoringModule.cs:412-428, 489-495), and connection records
persist via FsModule -- see area-authoring.md.

All persistence is opt-in: only entities in sessions with phase `Playing` or `LinkDead`
are included in autosave and shutdown saves. Entities in phase `Creating` are excluded.
(src/Tapestry.Engine/Persistence/PlayerPersistenceService.cs:54,
tests/Tapestry.Engine.Tests/Persistence/PlayerPersistencePhaseFilterTests.cs)

---

## Behavior

### Account model

- `AccountSaveData` stores: GUID `Id`, normalized lowercase `Email`, BCrypt
  `PasswordHash`, `Characters` list (character names), `CreatedAt`, `EmailVerified`,
  and `VerifiedAt`. Password is NOT stored in player saves -- it lives only in the
  account record. (src/Tapestry.Engine/Persistence/AccountSaveData.cs:1)

- `AccountService` hashes passwords with BCrypt on `CreateAccount` and verifies with
  `BCrypt.Verify` on `Authenticate` and `AuthenticateById`. Emails are trimmed and
  lowercased before storage and lookup.
  (src/Tapestry.Engine/Persistence/AccountService.cs:16-42)

- `AccountService` maintains an in-memory `_entityToAccount` dictionary mapping online
  entity GUIDs to account GUIDs during a session; characters are tracked per-account via
  `AddCharacterToAccount` / `RemoveCharacterFromAccount` (case-insensitive dedup).
  (src/Tapestry.Engine/Persistence/AccountService.cs:60-118)

- Minimum password length defaults to 6 characters, configurable via
  `persistence.password_min_length` in `server.yaml`. (src/Tapestry.Data/ServerConfig.cs:179)

### Player save structure

`PlayerSaveData` is a versioned DTO (Version = 1 at time of writing) that captures:

- Identity: `Id` (GUID string), `AccountId` (GUID string), `Name`, `Type` (default
  "player"), `Location` (room ID string). (src/Tapestry.Engine/Persistence/PlayerSaveData.cs:6-19)

- Tags and Roles: persisted as flat string lists; tags and roles are restored as-is.
  Admin role is stored in `Roles`, not `Tags` -- a save with "admin" in `Tags` but
  no `Roles` field will NOT grant the admin role on load.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:53-61,
  tests/Tapestry.Engine.Tests/Persistence/PlayerSerializerTests.cs:236)

- Stats: split into `Base` (Strength, Intelligence, Wisdom, Dexterity, Constitution,
  Luck, MaxHp, MaxResource, MaxMovement), `Vitals` (current Hp, Resource, Movement),
  and `Modifiers` (list of Source/Stat/Value triples). Modifiers are restored before
  vitals so clamping works correctly.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:137-166,258-283)

- Properties: the full dynamic property bag, filtered through `PropertyRegistry`.
  Transient properties are silently dropped at save time. Known properties use direct
  value serialization; unknown (unregistered) properties use a tagged-dict format
  `{type: "bool", value: true}` so the scalar type survives the YAML round-trip.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:168-223)

- Equipment: saved as a `Dictionary<string, string>` mapping slot name to item GUID.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:225-233)

- Inventory: top-level contents are stored as an ordered list of GUIDs; nested items
  (containers and their contents) are stored in a flat `Items` list with an optional
  `Container` GUID field to recreate the hierarchy on load.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:40-42, 69-126)

- Items carry: `Id`, `Name`, `Type`, `Container`, `Tags`, `Keywords`, and a
  `Properties` map. PlayerSpawner.cs:70-76 never rehydrates items from
  `template_id`; item stat modifiers are transient (InventoryProperties.cs:18)
  and are lost across a save/load cycle. This is a current limitation: any
  stat modifier applied to an item at runtime will not survive a server restart.
  (src/Tapestry.Engine/Persistence/PlayerSaveData.cs:56-65)

### PropertyRegistry pattern

- The `PropertyRegistry` is the single source of truth for which properties can be
  persisted, what types they hold, and whether they are transient.
  (src/Tapestry.Engine/Persistence/PropertyRegistry.cs:24)

- Engine properties are registered with `RegisterEngineProperty` using a bare snake_case
  name (must match `^[a-z][a-z0-9_]*$`, no hyphens).
  (src/Tapestry.Engine/Persistence/PropertyRegistry.cs:31-53)

- Pack properties are registered with `RegisterPackProperty`; the full key stored in
  the registry is `{pack}:{name}` (lowercase). Packs cannot shadow engine property
  names. (src/Tapestry.Engine/Persistence/PropertyRegistry.cs:55-82)

- A property registered with `transient: true` is excluded from serialization. Transient
  examples: `source_pack`, tell targets, follow/group membership, rest state.
  (src/Tapestry.Engine/CommonProperties.cs:34-46,
  src/Tapestry.Engine/Persistence/PlayerSerializer.cs:178)

- The serializer resolves pack-qualified keys without a pack context via
  `TryResolveByName`. Ambiguous bare names (same name in two packs) fall back to the
  legacy tagged format. (src/Tapestry.Engine/Persistence/PropertyRegistry.cs:128-177)

- Supported `PropertyValueType` values: String, Int, Double, Bool, Long, MapInt
  (`Dictionary<string,int>`), MapString (`Dictionary<string,string>`), ListString
  (`List<string>`). (src/Tapestry.Engine/Persistence/PropertyValueType.cs:1)

- Map properties (`MapInt`, `MapString`) round-trip correctly through the serializer.
  (tests/Tapestry.Engine.Tests/Persistence/MapPropertyTests.cs:88)

### Property key migrations (read-old-write-new)

- `PlayerSerializer` keeps a static list of (old key, new key) pairs and rewrites a
  legacy persisted property key to its current name as the property bag materializes
  on load. This is separate from `SaveMigrations` (whole-save version bumps, below):
  it runs per-property on every load, independent of save version, so a rename lands
  without needing a version bump or a corrective migration entry.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:PropertyKeyMigrations,
  DeserializeProperties)

- If a save carries both the old and new key for the same property, the old entry is
  dropped and the new key's value is kept. Once a migrated save is re-saved, only the
  new key is written -- the old key does not round-trip back in.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:MigratePropertyKey)

- Current pairs: `wimpy_threshold` -> `wimpy_pct` (combat's wimpy/flee-threshold
  unification; see combat-resolution.md). Extend the list for future renames -- no
  other code change is required.
  (src/Tapestry.Engine/Persistence/PlayerSerializer.cs:PropertyKeyMigrations,
  tests/Tapestry.Engine.Tests/Persistence/PlayerSerializerTests.cs:FromSaveData_MigratesLegacyWimpyThresholdKeyToWimpyPct)

### File-backed store layout

Both stores are configured from `server.yaml` `persistence.save_path` (default
`./data/saves`). Relative paths are resolved against the config file's directory.
(src/Tapestry.Data/ServerConfig.cs:175, src/Tapestry.Server/Persistence/FilePlayerStore.cs:18-26)

**Player files:**
- Per-character directory: `<save_path>/players/<name_lowercase>/`
- Primary save file: `<dir>/player.yaml` (YAML, underscored naming convention)
- Quest sidecar: `<dir>/quests.yaml` -- written by `QuestPersistenceService` separately
- `GetSupplementalFileTypes` enumerates other `.yaml` files in the player directory
  (any file not named `player.yaml`), so packs can add their own sidecar files.
  (src/Tapestry.Server/Persistence/FilePlayerStore.cs:114-132,
  tests/Tapestry.Engine.Tests/Persistence/FilePlayerStoreDirectoryTests.cs:76)

- Writes use an atomic tmp-then-rename pattern: data goes to `player.yaml.tmp`, the
  existing file is moved to `player.yaml.bak`, then the tmp is moved to `player.yaml`,
  then the bak is deleted. (src/Tapestry.Server/Persistence/FilePlayerStore.cs:79-101)

- Player name is lowercased for directory and file path construction. Path traversal
  attempts (e.g. `../etc/passwd`) throw `ArgumentException`.
  (src/Tapestry.Server/Persistence/FilePlayerStore.cs:134-153,
  tests/Tapestry.Engine.Tests/Persistence/FilePlayerStoreHashGuardTests.cs:33)

**Account files:**
- Per-account directory: `<save_path>/accounts/<guid>/`
- Account file: `<dir>/account.yaml`
- Email index: `<save_path>/accounts/index.yaml` -- maps lowercased email to account
  GUID. Loaded at startup into an in-memory dictionary; updated synchronously on every
  `SaveAsync`. Writes use a tmp-then-overwrite pattern (no bak step).
  (src/Tapestry.Server/Persistence/FileAccountStore.cs:49-92)

### Quest persistence

Quest state is written to `<save_path>/players/<name>/quests.yaml` by
`QuestPersistenceService`. It is loaded on `player.login` event and orphaned quest
entries (quests not in the registry) are stripped before restore.
(src/Tapestry.Engine/Quests/QuestPersistenceService.cs:52-113)

### Save triggers

- **Manual save**: the `save` command (priority 100, pack "core") calls
  `PlayerPersistenceService.SavePlayer` for the issuing session.
  (src/Tapestry.Server/Modules/PersistenceModule.cs:52-62)

- **Autosave**: a tick handler named `"autosave"` fires every
  `persistence.autosave_interval` ticks (default 300). It snapshots all eligible
  sessions synchronously, then flushes to disk asynchronously via `Task.Run`.
  (src/Tapestry.Server/Modules/TickHandlerModule.cs:161-171,
  src/Tapestry.Data/ServerConfig.cs:178)

- **Shutdown save**: `GameLoopService.StopAsync` calls `SaveAllPlayers` before
  notifying connected players and stopping the game loop.
  (src/Tapestry.Server/GameLoopService.cs:351)

- **New character save**: `FlowPersistenceAdapter.SaveNewPlayer` is called from the
  character creation flow on the connection input thread using
  `GetAwaiter().GetResult()`. (src/Tapestry.Server/Persistence/FlowPersistenceAdapter.cs:21-26)

- Only sessions in phase `Playing` or `LinkDead` are included in `SaveAllPlayers` and
  `SnapshotAllPlayers`. Phase `Creating` sessions are skipped.
  (src/Tapestry.Engine/Persistence/PlayerPersistenceService.cs:54-55)

### Migration support

`SaveMigrations.CurrentVersion` is 1. The migration dictionary is empty -- no
migrations have been defined yet. `FilePlayerStore.LoadAsync` logs an informational
message when it reads a save at a version below current but takes no corrective action
(migration is expected to be applied via the dictionary when populated).
(src/Tapestry.Server/Persistence/SaveMigrations.cs:1,
src/Tapestry.Server/Persistence/FilePlayerStore.cs:63-68)

### Seed players

- The admin seed (from `server.yaml` `admin` section) is created in
  `PlayerInitModule.Configure`, which runs before the registration seal.
  (src/Tapestry.Server/Modules/PlayerInitModule.cs:49-105)
- The seed password may be supplied out-of-band via the `TAPESTRY_ADMIN_PASSWORD`
  environment variable, which overrides the `admin.Password` config value when
  set and non-empty. When the seed falls back to a plaintext config password, a
  one-time warning is logged nudging operators toward the env var. The seed only
  runs when no save exists for the admin handle, so the password matters only on
  a fresh data store. (src/Tapestry.Server/Modules/PlayerInitModule.cs:62-70)

- Pack-declared seed players (from `players.yaml` in each pack directory) are created
  in `PlayerInitModule.LoadSeedPlayers`, called after `RegistrationPolicy.Resolve()`.
  If a save already exists for a seed player's name it is skipped.
  (src/Tapestry.Server/Modules/PlayerInitModule.cs:114-205)

### Scripting

The `DataModule` JS API (`data.loadYaml`) provides pack scripts read-only access to
YAML files within their own pack directory. It does not write to any persistent store
and has no connection to `PlayerSerializer` or `PropertyRegistry`.
(src/Tapestry.Scripting/Modules/DataModule.cs:22-52)

### Pack file persistence

Packs can write persistent files via `fs.writeYaml` and `fs.deleteFile`
(FsModule.cs:31-50). These writes go directly to the pack content directory on disk
and survive restarts. This is used by the authoring system (area side-cars, connection
records) but is available to any pack.

### SpawnManager persistent flag

The `persistent` flag on a mob spawn rule (SpawnManager) is a respawn-cap behavior:
it prevents the mob from respawning once it has been killed. It is not a disk-
persistence mechanism and has no connection to the save/load system described above.

---

## Rejected and Reverted

- None on record.

---

## Change Log

- 2026-06-13 [auth-surface-hardening](changes/2026-06-13-auth-surface-hardening.md)
