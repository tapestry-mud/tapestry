# Tapestry Web Client

Browser-based MUD client for Tapestry. React 19 + TypeScript, Zustand state management, xterm.js terminal, WebSocket connection with GMCP structured data.

Upcoming work is tracked in the [issue tracker](https://github.com/tapestry-mud/tapestry/issues?q=label%3Aclient).

---

## Completed Phases

### Phase 1: MVP

WebSocket client connecting directly to Tapestry server. xterm.js terminal with ANSI color rendering. CommandBar input with command history. Debug drawer. ConnectScreen with recent server list. GMCP handlers for Char.Vitals, Char.Status, Room.Info, Comm.Channel, Room.WrongDir.

### Phase 1.1: Three-Column Layout

Resizable three-column layout via react-resizable-panels. Left: CharacterPanel, VitalsPanel, EffectsPanel, XPPanel. Center: RoomViewPanel, OutputViewport. Right: EquipmentPanel, InventoryPanel, HotbarPanel, NearbyPanel. Bottom: CommandBar, ChatDrawer. Four themes with ANSI color palettes. Per-character theme persistence via localStorage.

### Phase 1.2: Login Phase & Security

GMCP `Char.Login.Phase` signal driving layout transitions (ConnectScreen -> LoginLayout -> GameLayout). CommandBar masks input during password phase. Password confirmation on character creation. `resetpassword` converted to a gated prompt flow.

### Phase 2.1: Room & Map

Room.Info expanded with description and environment. Room.Nearby GMCP package for live entity list. Auto-mapper rendering roomStore graph. RoomViewPanel and NearbyPanel wired to live data. Drag-and-drop panel layout via layoutStore. Hotbar moved to center column.

### Phase 2.2: Character & Progression

Char.Status expanded with attributes, alignment, gold, hunger tier. New Char.Experience package for per-track XP data. CharacterPanel, XPPanel wired to live data.

### Phase 2.3: Effects & Combat Info

Char.Effects (active buffs/debuffs). Char.Combat.Target (combat target with health tier). isAdmin in Char.Status. HealthTier utility. EffectsPanel and CombatTargetPanel live. `affects` command added server-side.

### Phase 2.4: Inventory & Equipment

Char.Items (inventory with display-only template_id stacking) and Char.Equipment GMCP packages. InventoryPanel and EquipmentPanel wired to live data.

### Response GMCP Architecture

Typed `Response.*` GMCP namespace. Three-tier emit model: suppressed, auto-feedback, typed response. Auto-feedback on sendToPlayer makes every command accessible by default. Curated schemas for shop, training, score, look, help. GmcpDispatcher routing with catch-all fallback.

### Accessibility

Login.Prompt, Flow.Step, and Flow.Help GMCP events for screen reader support during login and character creation. Shortcut engine (Alt+L room description, Alt+C context commands) with rebindable keys and localStorage persistence. Room-entry hints stashed from GMCP and announced on arrival. Announcement categories with assertive/polite/off preferences. Mobile layout shows terminal only (GMCP panels remain in DOM as sr-only).

---

## Tech Stack

- React 19 + TypeScript + Vite
- Zustand (state management)
- Zod (GMCP schema validation)
- xterm.js + FitAddon (terminal)
- react-resizable-panels (layout)
- Tailwind CSS (styling)
- Vitest (testing)
