# Tapestry Web Client Roadmap

Browser-based MUD client for Tapestry. React 19 + TypeScript, Zustand state management, xterm.js terminal, WebSocket connection with GMCP structured data.

---

## Phase Summary

| Phase | Name | Status | Scope |
|-------|------|--------|-------|
| ~~1~~ | ~~MVP~~ | Done | WebSocket, xterm.js, ANSI rendering, text I/O |
| ~~1.1~~ | ~~Three-Column Layout~~ | Done | Resizable panels, theme system (dark/light/midnight/amber), per-character settings |
| ~~1.2~~ | ~~Login Phase & Security~~ | Done | Char.Login.Phase GMCP, password masking, LoginLayout, session input modes, resetpassword prompt flow |
| ~~2.1~~ | ~~Room & Map~~ | Done | Room.Info/Nearby GMCP, auto-mapper with corridor grid, drag-and-drop panels, time/weather, description tag colorizer |
| ~~2.2~~ | ~~Character & Progression~~ | Done | Char.Status expansion, Char.Experience, score/XP panels, animated gradient bars |
| ~~2.3~~ | ~~Effects & Combat Info~~ | Done | Char.Effects, health tier on examine + GMCP, combat target panel, admin flag, `affects` command |
| **2.4** | **Inventory & Equipment** | Up Next | Char.Items, Char.Equipment, item stacking backend |
| 2.5 | Hotbar Polish | | Drag commands/items onto hotbar slots, context-menu shortcuts |
| 3 | Mapper Polish | | Click-to-walk, area grouping, zoom, minimap toggle |
| 4 | Chat Polish | | Channel tabs, filtering, imm channel gated by admin flag |
| 5 | Settings & Deploy | | Per-character settings expansion, Docker stack |
| 6 | User Scripting | | Web Worker JS sandbox, triggers, aliases, timers, GMCP hooks |

---

## Done

### Phase 1: MVP

WebSocket client connecting directly to Tapestry server (no TCP bridge needed -- server has native WebSocket support). xterm.js terminal with ANSI color rendering, CommandBar input with command history (in-memory, 100 entries), debug drawer (backtick toggle). ConnectScreen with recent server list (localStorage). GMCP handlers for Char.Vitals, Char.Status, Room.Info (partial), Comm.Channel, Room.WrongDir.

### Phase 1.1: Three-Column Layout

Resizable three-column layout via react-resizable-panels (25/50/25 default). Left: CharacterPanel, VitalsPanel, EffectsPanel, CombatStatsPanel, XPPanel. Center: RoomViewPanel, OutputViewport. Right: EquipmentPanel, InventoryPanel, HotbarPanel, NearbyPanel. Bottom: CommandBar, ChatDrawer. Four themes with ANSI color palettes. Per-character theme persistence via localStorage keyed by `tapestry:settings:{characterName}`. PackRegistry extension points for sidebar panels.

### Phase 1.2: Login Phase & Security

GMCP `Char.Login.Phase` signal (name/password/creating/playing). Server sends phase at each login transition. Web client switches between ConnectScreen, LoginLayout (terminal-only, no panels), and GameLayout based on phase. CommandBar masks input (`type="password"`) during password phase, suppresses echo to terminal, skips command history. Session InputMode (Normal/Prompt) on server gates post-login sensitive flows. Password confirmation on character creation (3 failures disconnect). `resetpassword` converted to prompt flow with safe zone gate, current password verification, 1-failure exit.

---

## Done (continued)

### Phase 2.1: Room & Map

Wire the center column panels to real data and make the layout customizable. The map panel and room view are the most visible broken pieces -- hardcoded descriptions and no rendered map despite the roomStore already building a graph.

**Layout customization:**

- Panels in the left and right columns become re-arrangeable. Players can drag panels between left/right columns and reorder within a column. Center column stays fixed (MapPanel, RoomViewPanel, OutputViewport, HotbarPanel, CommandBar).
- New `layoutStore` (Zustand, persisted to localStorage keyed by character name) tracks panel assignments: `{ panels: [{ id, column: 'left' | 'right', order: number }] }`. Default layout matches current hardcoded positions.
- `GameLayout.tsx` reads from layoutStore instead of hardcoding panel lists. Each panel registers with an `id`, `component`, and `defaultColumn`/`defaultOrder`.
- Reorder UI: drag-and-drop via `@dnd-kit/core` (lightweight, React-native, accessible) or a simpler "panel settings" modal with up/down/move-column buttons. Drag-and-drop is nicer but adds a dependency -- settings modal is zero-dependency. Decide during spec.
- PackRegistry panels also get column/order assignments so pack-contributed panels participate in the layout system.

**Backend GMCP changes:**

- **Expand Room.Info** -- add `description` (string) and `environment` (string, e.g. "indoor", "forest", "city") to the existing payload. Room.Description is already on the Room object (`Room.cs:10`) but not sent in `GmcpService.SendRoomInfo`. Also add `doors` dict for locked/closed door state on exits (Exit.Door exists but isn't sent).
- **New: Room.Nearby** -- list of visible entities in the room. Payload: `{ entities: [{ name, type, tags? }] }`. Sent on room entry and whenever an entity enters/leaves the room. The `player.moved` event (`ApiWorld.cs:119`) fires on movement -- subscribe to send Room.Nearby updates to all players in both the old and new rooms.
- **Room.Info trigger on entity movement** -- currently Room.Info is only sent on player login and when the player moves. Other entities entering/leaving don't trigger an update. Room.Nearby handles this separately.

**Client changes:**

- **RoomViewPanel** -- remove `FAKE_DESCRIPTION` and `FAKE_ENTITIES`. Wire description from roomStore (add `description` field). Wire entity list from nearbyStore.
- **NearbyPanel** -- remove `FAKE_NEARBY`. Wire to nearbyStore populated by Room.Nearby handler.
- **Map panel** -- the roomStore already builds a `mapGraph: Map<roomId, RoomNode>` with spatial coordinates from Room.Info exits. Render this as a visual map (canvas or SVG). Current room highlighted. Exits shown as connections. This is the auto-mapper -- no click-to-walk yet (that's Phase 3).
- **Exits** -- verify exits render correctly in RoomViewPanel. The data is there (`roomStore.currentRoom.exits`) but may not be displayed. Compass buttons already send direction commands.
- **Hotbar relocation** -- move HotbarPanel from right column to center column, above CommandBar. This puts action buttons near the input where they're most useful.
- **GMCP schema** -- add `RoomNearbySchema` to `types/gmcp.ts`. Update `RoomInfoSchema` to include optional `description` and `environment` fields (backward compatible -- old servers won't send them).

**Key files:**

| Side | File | Change |
|------|------|--------|
| Server | `GmcpService.cs` | Add description/environment to SendRoomInfo, add SendRoomNearby method |
| Server | `GmcpService.cs` | Subscribe to player.moved event to push Room.Nearby to affected rooms |
| Client | `types/gmcp.ts` | Update RoomInfoSchema, add RoomNearbySchema |
| Client | `stores/roomStore.ts` | Add description field |
| Client | `stores/nearbyStore.ts` | Already exists -- wire to GMCP handler |
| Client | `connection/GmcpDispatcher.ts` | Register Room.Nearby handler |
| Client | `panels/RoomViewPanel.tsx` | Remove hardcoded data, wire to stores |
| Client | `panels/NearbyPanel.tsx` | Remove FAKE_NEARBY, wire to store |
| Client | `panels/MapPanel.tsx` | New -- render roomStore.mapGraph |
| Client | `stores/layoutStore.ts` | New -- panel column/order assignments, persisted per character |
| Client | `layout/GameLayout.tsx` | Read panel layout from layoutStore, add MapPanel to center, move HotbarPanel above CommandBar |

---

## Done (continued)

### Phase 2.2: Character & Progression

Wire the left column character panels to real data from expanded GMCP packages.

**Backend GMCP changes:**

- **Expand Char.Status** -- add attributes (str/int/wis/dex/con/luck), alignment (value + bucket), gold, hunger tier. All this data is available on the entity and displayed by the `score` command (`packs/tapestry-core/scripts/commands/utility.js:40-156`). Send on login and whenever stats change (level up, equipment change, buff/debuff).
- **New: Char.Experience** -- per-track progression data. Payload: `{ tracks: [{ name, level, xp, xpToNext, currentLevelThreshold }] }`. Subscribe to `progression.xp.gained` and `progression.level.up` events (`ProgressionManager.cs`) to push updates.

**Client changes:**

- **CharacterPanel** -- expand to show race, class, level, alignment bucket, gold. Data comes from charStore via expanded Char.Status.
- **XPPanel** -- remove hardcoded data. Wire to new xpStore populated by Char.Experience. Show progress bar per track.
- **Score/Stats display** -- show STR/INT/WIS/DEX/CON/LUK from charStore. Could be a new StatsPanel or integrated into CharacterPanel.
- **GMCP schemas** -- update CharStatusSchema with optional new fields. Add CharExperienceSchema.

**Key files:**

| Side | File | Change |
|------|------|--------|
| Server | `GmcpService.cs` | Expand SendCharStatus payload, add SendCharExperience method |
| Server | `GmcpService.cs` | Subscribe to progression events to push Char.Experience |
| Client | `types/gmcp.ts` | Update CharStatusSchema, add CharExperienceSchema |
| Client | `stores/charStore.ts` | Add attributes, alignment, gold, hunger fields |
| Client | `stores/xpStore.ts` | New store for progression track data |
| Client | `connection/GmcpDispatcher.ts` | Register Char.Experience handler |
| Client | `panels/CharacterPanel.tsx` | Wire expanded charStore fields |
| Client | `panels/XPPanel.tsx` | Remove hardcoded data, wire to xpStore |

---

## Done (continued)

### Phase 2.3: Effects & Combat Info

Wire effects and combat target data to the web client. Add health tier system for both telnet and GMCP.

**Backend:**

- **HealthTier utility** (`HealthTier.cs`) -- maps HP% to tier string ("perfect", "few scratches", "small wounds", "wounded", "badly wounded", "bleeding profusely", "near death") and display text. Used by GMCP and telnet examine.
- **Char.Effects** -- active buffs/debuffs. Payload: `{ effects: [{ id, name, remainingPulses, flags, type }] }`. Pushed on `effect.applied`, `effect.removed`, `effect.expired` events and on login.
- **Char.Combat.Target** -- current combat target. Payload: `{ active, name, healthTier, healthText }`. Pushed on `combat.engage`, `combat.hit`, `combat.end`, `combat.kill` events.
- **isAdmin in Char.Status** -- `entity.HasTag("admin")` sent with existing payload.
- **healthTier in Room.Nearby** -- each entity in the room now includes `healthTier`. Updated on `combat.hit`.
- **Health tier on examine** -- `look <entity>` shows condition text for NPCs, mobs, and players. Combat status in room shows health tier in parentheses.
- **`affects` command** -- telnet command showing active effects in Panel frame with duration.
- **NullGmcpModuleAdapter** -- default no-op adapter for test DI; server overrides with real adapter.

**Client:**

- **EffectsPanel** -- wired to real `affectsStore` via `Char.Effects` handler. Shows buff/debuff icon, name, pulse countdown ("perm" for permanent).
- **CombatTargetPanel** (new) -- slim panel showing target name + health bar with tier-colored gradient. Only visible during active combat.
- **NearbyPanel** -- shows health tier in parentheses for damaged entities.
- **CombatStatsPanel removed** -- hitroll/damroll/AC/speed panel dropped (not useful to players).
- **DebugDrawer** -- gated behind `isAdmin` from charStore.

---

## Up Next

### Phase 2.4: Inventory & Equipment

Wire inventory and equipment panels. Requires backend item stacking work.

**Backend changes:**

- **Item stacking** -- currently every item is a separate Entity in `entity.Contents`. For GMCP display, group items by `template_id` property and send as `{ name, templateId, quantity, weight }`. The stacking is display-only on the GMCP side -- the engine still stores individual entities. The grouping happens in the GMCP serialization layer, not in the entity model.
- **New: Char.Items** -- inventory list. Payload: `{ items: [{ id, name, templateId?, quantity, weight?, type }] }`. Items with the same `template_id` are grouped with a quantity count. Items without a template_id (unique items) get quantity 1. Subscribe to `entity.item.picked_up`, `entity.item.dropped`, `entity.item.given`, `container.item_added` events to push updates.
- **New: Char.Equipment** -- equipped item slots. Payload: `{ slots: { "head": { id, name, modifiers? }, "weapon": { id, name, modifiers? }, ... } }`. Read from `entity.Equipment` dict (`Entity.cs:92-105`). Subscribe to `entity.equipped` and `entity.unequipped` events. Multi-slot items use indexed keys (e.g., `"finger:0"`, `"finger:1"`).

**Client changes:**

- **InventoryPanel** -- remove FAKE_ITEMS fallback. Wire to inventoryStore populated by Char.Items. Show item name, quantity (if > 1), type icon.
- **EquipmentPanel** -- remove FAKE_SLOTS. Wire to equipmentStore populated by Char.Equipment. Show slot layout with equipped item names. equipmentStore already has the 12-slot structure (`equipmentStore.ts`).
- **GMCP schemas** -- add CharItemsSchema and CharEquipmentSchema.

**Key files:**

| Side | File | Change |
|------|------|--------|
| Server | `GmcpService.cs` | Add SendCharItems (with template_id grouping), SendCharEquipment |
| Server | `GmcpService.cs` | Subscribe to inventory/equipment events |
| Client | `types/gmcp.ts` | Add CharItemsSchema, CharEquipmentSchema |
| Client | `stores/inventoryStore.ts` | Wire to GMCP handler (store exists) |
| Client | `stores/equipmentStore.ts` | Wire to GMCP handler (store exists) |
| Client | `connection/GmcpDispatcher.ts` | Register Char.Items, Char.Equipment handlers |
| Client | `panels/InventoryPanel.tsx` | Remove FAKE_ITEMS, wire to inventoryStore |
| Client | `panels/EquipmentPanel.tsx` | Remove FAKE_SLOTS, wire to equipmentStore |

---

### Phase 2.5: Hotbar Polish

Make hotbar slots assignable from the UI rather than only via right-click configuration.

**From CommandsDrawer:**

- Right-click a command entry (or long-press on touch) to get a context menu: "Add to hotbar". Opens slot picker showing current hotbar state; clicking an empty slot (or confirming overwrite of an occupied one) sets `{ emoji: first char of keyword as emoji fallback, label: keyword[0..3], command: keyword }`.
- Drag a command row directly onto a hotbar slot (dnd-kit draggable source, hotbar slots as drop targets).

**From InventoryPanel (depends on Phase 2.4):**

- Same right-click context menu on inventory items: "Add to hotbar". Sets `{ emoji: item type icon, label: item name[0..3], command: 'use ' + itemName }` or a sensible default verb (`quaff`, `eat`, `wield`, `wear`) inferred from item type.
- Drag an inventory item row onto a hotbar slot.

**Hotbar UX improvements:**

- Drag to reorder slots within the hotbar (dnd-kit sortable, already available in the dep tree).
- Drag a slot off the hotbar to clear it (drop outside the bar).
- Slot tooltip on hover: show full command string, not just label.

**Key files:**

| File | Change |
|------|--------|
| `controls/Hotbar.tsx` | Add drop targets per slot; add drag-to-reorder; slot clear on outside drop |
| `panels/CommandsDrawer.tsx` | Add draggable command rows + right-click context menu |
| `panels/InventoryPanel.tsx` | Add draggable item rows + right-click context menu (Phase 2.4 prereq) |
| `stores/hotbarStore.ts` | No change needed -- setSlot/clearSlot already cover it |

---

### Phase 3: Mapper Polish

Enhance the auto-mapper from Phase 2.1 with interactive features.

- Click-to-walk (click a room node to pathfind and auto-walk)
- Area grouping (color-code rooms by area)
- Zoom in/out with scroll wheel
- Minimap vs fullscreen toggle
- Room labels (name or area:id)
- Biome-aware styling (if engine biome system lands -- color/icon per biome type)

---

### Phase 4: Chat Polish

- Channel tabs (split channels into separate scrollable views)
- Channel filtering (show/hide specific channels)
- Imm channel visibility gated by `isAdmin` from charStore (admin flag added in Phase 2.3)
- Clickable links in chat messages (MXP, if engine supports it later)
- Unread indicators per channel

---

### Phase 5: Settings & Deploy

- Expand per-character settings (font size, panel visibility, hotbar config per character)
- Command history persistence to localStorage
- Export/import settings
- Docker stack: client served alongside Tapestry server
- Production build optimization

---

### Phase 6: User Scripting

JavaScript scripting engine for power users -- triggers, aliases, timers, and GMCP hooks. Mirrors the Mudlet/TinTin++ experience but in the browser with no plugins.

**Architecture:**

- **Web Worker sandbox** -- user scripts run in an isolated Worker thread. A bad script can't freeze the UI or access the DOM. Communication is strictly via `postMessage` with a defined API contract.
- **`tapestry` API object** exposed inside the Worker, modeled after the server-side pack scripting pattern:
  - `tapestry.triggers.add(name, regex, callback)` -- fire callback when terminal output matches regex
  - `tapestry.aliases.add(name, pattern, replacement)` -- rewrite commands before sending (e.g., `kk` becomes `kill kobold`)
  - `tapestry.timers.add(name, intervalMs, callback)` -- repeating timer (e.g., auto-quaff at interval)
  - `tapestry.gmcp.on(package, callback)` -- subscribe to GMCP events (e.g., auto-eat when hunger tier changes)
  - `tapestry.send(command)` -- send a command to the server
  - `tapestry.echo(text)` -- write text to terminal locally (not sent to server)
  - `tapestry.log(message)` -- write to debug drawer
- **Data flow:** main thread sends terminal text lines and GMCP events to Worker via postMessage. Worker runs trigger/GMCP callbacks, sends commands back via postMessage. Main thread executes the commands through WebSocketClient.
- **Script storage** -- per-character localStorage (same keying as theme/hotbar). Scripts are plain JS text, editable in a script editor panel (CodeMirror or Monaco for syntax highlighting, or a simple textarea to start).
- **Script management UI** -- list of scripts with enable/disable toggle, edit, delete. Import/export as JSON for sharing.

**Key files:**

| File | Purpose |
|------|---------|
| `workers/scriptWorker.ts` | Web Worker -- loads user scripts, exposes tapestry API, handles postMessage protocol |
| `scripting/ScriptManager.ts` | Main thread coordinator -- bridges Worker with WebSocketClient, terminal, GMCP dispatcher |
| `scripting/scriptStore.ts` | Zustand store for script list, per-character persistence |
| `panels/ScriptEditorPanel.tsx` | UI for editing/managing scripts |
| `connection/GmcpDispatcher.ts` | Add hook to forward GMCP events to ScriptManager |
| `connection/WebSocketClient.ts` | Add hook to forward terminal text to ScriptManager |

**No backend dependency.** Entirely client-side.

**Future considerations:**
- **Pack-shipped client scripts** -- content packs could include a `client/` folder with triggers, aliases, and UI hooks that auto-load when the pack is active. A DCC pack ships spectator triggers, a LF pack ships combat macros. The script loader would need to distinguish user scripts (editable, per-character) from pack scripts (read-only, per-pack).
- **Electron/VS Code wrapper** -- embedding the web client in Electron gives file system access for real script files, native notifications, IntelliSense for the `tapestry` API, and an extension marketplace pattern for pack distribution. The Electron shell also unlocks tooling apps beyond the game client: area editor (visual room builder, exit wiring, mob/item placement writing YAML to pack `areas/` folder), pack editor (manifest editing, script authoring with API autocomplete), live preview via existing admin commands (`set`, `grant`, `inspect`) against a running server. Phase 7+ territory.

---

### Future: Engine-Dependent Client Features

These client features depend on engine systems that don't exist yet. Listed here so client contributors know what's coming.

| Engine Feature | Client Opportunity |
|---|---|
| Quest system (engine Phase 20.5) | Quest log panel, quest tracker in sidebar, step progress |
| Biome & visibility (engine TBD) | Darkness overlay on map, light source indicator, biome icons |
| NPC conversations (engine TBD) | Dialogue panel with choice buttons (replaces terminal-only dialogue) |
| Buff spec overhaul (engine TBD) | Richer effects panel with trigger countdown, buff categories |
| Pet/companion system (engine TBD) | Pet status panel, pet command hotbar |
| Area editor (engine TBD) | Visual room/exit editor in web client via GMCP. Room creation, exit wiring, mob/item/spawn placement. Writes individual YAML files (depends on engine one-file-per-entity format) |

---

## Engine Phase Dependencies

| Client Phase | Requires Engine Work |
|---|---|
| 2.1 (Room & Map) | Expand Room.Info payload, new Room.Nearby GMCP package |
| 2.2 (Character) | Expand Char.Status payload, new Char.Experience package |
| 2.3 (Effects & Combat) | New Char.Effects, Char.Combat, Char.Combat.Target packages, isAdmin in Char.Status |
| 2.4 (Inventory & Equipment) | New Char.Items (with display stacking), Char.Equipment packages |
| 3 (Mapper) | None -- client-only (biome styling needs engine biome system) |
| 4 (Chat) | None -- admin flag done in 2.3 |
| 5 (Deploy) | Docker/CI (engine Phase 16.75) |
| 6 (Scripting) | None -- client-only |
| Future: Quest Panel | Engine Phase 20.5 (quest system) |
| Future: Dialogue Panel | Engine TBD (NPC conversations) |
| Future: Darkness/Light | Engine TBD (biome & visibility) |
| Future: Area Editor | Engine TBD (area editor + one-file-per-entity) |

## Tech Stack

- React 19 + TypeScript + Vite
- Zustand (state management)
- Zod (GMCP schema validation)
- xterm.js + FitAddon (terminal)
- react-resizable-panels (layout)
- Tailwind CSS (styling)
- Vitest (testing)
