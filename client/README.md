# Tapestry Web Client

Browser-based MUD client for the Tapestry engine. React 19 + TypeScript, xterm.js terminal, WebSocket with GMCP structured data.

## Quick Start

```bash
npm install
npm run dev
```

Connect to a running Tapestry server (default `localhost:4000`).

## Accessibility

The client is designed to work with screen readers and assistive technology. No separate "accessible mode" -- accessibility features are always active and invisible to sighted users.

### Screen Reader Experience

On page load, a screen reader user tabs through skip links ("Skip to game output", "Skip to command input", "Announcement settings") then lands on the command input. From there, gameplay is hands-free: type commands and listen. Game output is announced automatically via xterm's screen reader mode, and game state changes fire as live region announcements.

### Features

**Skip navigation** -- three links at the top of the page, visible only on keyboard focus:
- Skip to game output (the terminal)
- Skip to command input (the command bar)
- Announcement settings (opens settings and focuses the configuration)

**Live announcements** -- game events are announced via ARIA live regions without requiring navigation:
- Vitals warnings when HP, mana, or movement drop below 40% (low) or 10% (critical)
- Combat start/end notifications
- Chat messages with sender and channel
- Room changes (when wired)

Each category is independently configurable as Interrupt (assertive), Polite, or Off via the settings panel. Preferences persist to localStorage.

**Vitals debouncing** -- alerts fire once when crossing a threshold downward, then stay silent until the player heals above 40%. This prevents announcement spam during combat where vitals update every battle tick.

**xterm screen reader mode** -- terminal output is mirrored to a live text buffer that screen readers can traverse. This is always on.

**Semantic markup** -- `<main>` landmark for the game area, `role="log"` on the terminal output, `role="complementary"` on mobile panel containers, `aria-label` on inputs and interactive elements, `aria-pressed` on toggle buttons.

**Mobile layout** -- on screens under 768px, GMCP sidebar panels are visually hidden using the `sr-only` pattern (CSS clip-rect, not `display: none`). Panels remain in the DOM and are readable by assistive technology. The terminal gets full viewport width.

### Adding New Announcement Categories

1. Add the category key to `AnnounceCategory` in `stores/announcePrefsStore.ts`
2. Set a default priority in the `DEFAULTS` object
3. Add a label entry in `ANNOUNCE_CATEGORIES` in `controls/SettingsModal.tsx`
4. Call `announce(message, 'your-category')` from wherever the event fires

### Architecture

```
src/accessibility/
  announceStore.ts   -- Zustand store + announce() helper, respects user prefs
  Announcer.tsx      -- Two sr-only live regions (assertive + polite)
  SkipLinks.tsx      -- Skip navigation links, visible on focus
  vitalAlerts.ts     -- Threshold tracker for HP/mana/mv with debounce

src/stores/
  announcePrefsStore.ts -- Per-category priority preferences, persisted to localStorage

src/hooks/
  useIsMobile.ts     -- Media query hook for mobile breakpoint detection
```

## Tech Stack

- React 19 + TypeScript + Vite
- Zustand (state management)
- Zod (GMCP schema validation)
- xterm.js + FitAddon (terminal)
- react-resizable-panels (layout)
- @dnd-kit (drag-and-drop panel reordering)
- Tailwind CSS v4 (styling)
- Vitest + React Testing Library (testing)

## Roadmap

See [ROADMAP.md](ROADMAP.md) for the full phase tracking.
