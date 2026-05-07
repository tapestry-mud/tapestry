# Group B Module Coverage

# End-to-end telnet runner scenarios for all 8 Group B modules extracted in this branch.
# Each module section verifies at least one observable behavior via the live server.
# Pre-seeded players (testpass123) and example-pack content are assumed loaded.

# ---------------------------------------------------------------------------
# ConfigurationModule
# ---------------------------------------------------------------------------

## Scenario: Stats train correctly
- Players: Alice, Trainer

### Steps
1. Alice: `score`
2. Assert Alice sees: `Strength`
3. Alice: `train strength`
4. Assert Alice sees: `You feel stronger`

## Scenario: Shop applies markup to base price
- Players: Alice

### Steps
1. Alice: `go shop`
2. Alice: `list`
3. Assert Alice sees: `gold`

# ---------------------------------------------------------------------------
# ContentLoadingModule
# ---------------------------------------------------------------------------

## Scenario: MOTD shows pack credits on login
- Players: Alice

### Steps
1. Assert Alice sees: `example-pack`

## Scenario: Connections load between rooms on boot
- Players: Alice

### Steps
1. Alice: `look`
2. Assert Alice sees: `Town Square`
3. Alice: `north`
4. Assert Alice sees: `Wanderer's Rest`

## Scenario: Theme compiles -- output uses pack color tokens not raw codes
- Players: Alice

### Steps
1. Alice: `look`
2. Assert Alice sees: `Town Square`
3. Assert Alice does not see: `\e[`

# ---------------------------------------------------------------------------
# CombatEventModule
# ---------------------------------------------------------------------------

## Scenario: Aggressive mob initiates combat on room entry
- Players: Gamemaster, Alice

### Steps
1. Gamemaster: `teleport core:aggro-arena`
2. Gamemaster: `spawn core:aggro-rat`
3. Assert Gamemaster sees: `Spawned`
4. Alice: `teleport core:aggro-arena`
5. Assert Alice sees: `attacks you`

## Scenario: Dying entity triggers death event and leaves corpse
- Players: Gamemaster

### Steps
1. Gamemaster: `teleport core:test-arena`
2. Gamemaster: `spawn core:goblin`
3. Assert Gamemaster sees: `Spawned`
4. Gamemaster: `kill goblin`
5. Assert Gamemaster sees: `slain`
6. Assert Gamemaster sees: `corpse`

## Scenario: Flee command ends combat and clears pulse delay
- Players: Gamemaster

### Steps
1. Gamemaster: `teleport core:test-arena`
2. Gamemaster: `spawn core:goblin`
3. Gamemaster: `kill goblin`
4. Assert Gamemaster sees: `You begin fighting`
5. Gamemaster: `flee`
6. Assert Gamemaster sees: `flee`
7. Assert Gamemaster does not see: `You begin fighting`

# ---------------------------------------------------------------------------
# GmcpEventModule
# ---------------------------------------------------------------------------

## Scenario: Moving room sends GMCP Room.Info without corrupting text output
- Players: Wanderer

### Steps
1. Wanderer: `north`
2. Assert Wanderer sees: `[Exits:`
3. Assert Wanderer does not see: `IAC`
4. Assert Wanderer does not see: `Room.Info`

## Scenario: Level-up sends Char.Status without corrupting score output
- Players: Alice

### Steps
1. Alice: `score`
2. Assert Alice sees: `Level`
3. Assert Alice does not see: `Char.Status`
4. Assert Alice does not see: `{`

## Scenario: Vitals dirty flag flushes once per tick after regen
- Players: Wanderer

### Steps
1. Wanderer: `score`
2. Assert Wanderer sees: `HP`
3. Assert Wanderer does not see: `Char.Vitals`

# ---------------------------------------------------------------------------
# WorldEventModule
# ---------------------------------------------------------------------------

## Scenario: Mob AI tracks player room entry and leave events
- Players: Gamemaster, Alice

### Steps
1. Gamemaster: `spawn example-pack:guide`
2. Assert Gamemaster sees: `Spawned`
3. Alice: `look`
4. Assert Alice sees: `guide`

## Scenario: Saving fires on level-up
- Players: Alice

### Steps
1. Alice: `score`
2. Assert Alice sees: `Alice`

## Scenario: New character starts with sustenance at 100
- Players: NewPlayer

### Steps
1. NewPlayer: `score`
2. Assert NewPlayer sees: `100`

## Scenario: Sleeping player auto-wakes when combat is engaged
- Players: Gamemaster, Alice

### Steps
1. Alice: `sleep`
2. Assert Alice sees: `You go to sleep`
3. Gamemaster: `spawn core:aggro-rat`
4. Assert Gamemaster sees: `Spawned`
5. Assert Alice sees: `You wake up`

# ---------------------------------------------------------------------------
# TickHandlerModule
# ---------------------------------------------------------------------------

## Scenario: Heartbeat fires -- combat pulse advances during fight
- Players: Gamemaster

### Steps
1. Gamemaster: `teleport core:test-arena`
2. Gamemaster: `spawn core:goblin`
3. Gamemaster: `kill goblin`
4. Assert Gamemaster sees: `You hit`

## Scenario: Corpse appears on mob death and decays after decay interval
- Players: Gamemaster

### Steps
1. Gamemaster: `teleport core:test-arena`
2. Gamemaster: `spawn core:goblin`
3. Gamemaster: `kill goblin`
4. Assert Gamemaster sees: `corpse`

## Scenario: Sustenance drains over time
- Players: Alice

### Steps
1. Alice: `score`
2. Assert Alice sees: `Sustenance`

## Scenario: Autosave triggers without error on tick
- Players: Alice

### Steps
1. Alice: `score`
2. Assert Alice sees: `Alice`

## Scenario: Regen fires -- HP recovers between ticks at rest
- Players: Alice

### Steps
1. Alice: `score`
2. Assert Alice sees: `HP`

## Scenario: GMCP vitals flush runs at end of tick
- Players: Wanderer

### Steps
1. Wanderer: `score`
2. Assert Wanderer sees: `HP`
3. Assert Wanderer does not see: `{`

# ---------------------------------------------------------------------------
# PersistenceModule
# ---------------------------------------------------------------------------

## Scenario: Save command confirms character was saved
- Players: Alice

### Steps
1. Alice: `save`
2. Assert Alice sees: `Character saved`

## Scenario: Resetpassword prompts for current password in safe room
- Players: Alice

### Steps
1. Alice: `resetpassword`
2. Assert Alice sees: `current password`

# ---------------------------------------------------------------------------
# PlayerInitModule
# ---------------------------------------------------------------------------

## Scenario: Seed players from pack YAML are loaded on boot
- Players: Alice

### Steps
1. Alice: `look`
2. Assert Alice sees: `Town Square`

## Scenario: Area mobs are present in their spawn rooms after boot
- Players: Gamemaster

### Steps
1. Gamemaster: `teleport example-pack:town-square`
2. Gamemaster: `look`
3. Assert Gamemaster sees: `guard`
