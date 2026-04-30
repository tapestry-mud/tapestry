import { create } from 'zustand'
import type { ITheme } from '@xterm/xterm'
import { getTerminal } from '../terminal/terminalStore'

export type Theme = 'dark' | 'light' | 'midnight' | 'amber'

const VALID_THEMES: Theme[] = ['dark', 'light', 'midnight', 'amber']
const NON_DARK_THEMES: Theme[] = ['light', 'midnight', 'amber']

const ANSI_COLORS = {
  black: '#000000',
  red: '#aa0000',
  green: '#00aa00',
  yellow: '#aa5500',
  blue: '#0000aa',
  magenta: '#aa00aa',
  cyan: '#00aaaa',
  white: '#aaaaaa',
  brightBlack: '#555555',
  brightRed: '#ff5555',
  brightGreen: '#55ff55',
  brightYellow: '#ffff55',
  brightBlue: '#5555ff',
  brightMagenta: '#ff55ff',
  brightCyan: '#55ffff',
  brightWhite: '#ffffff',
}

export const XTERM_THEMES: Record<Theme, ITheme> = {
  dark: {
    background: '#0d0d1a',
    foreground: '#e0e0e0',
    cursor: '#5b8a9a',
    selectionBackground: '#5b8a9a44',
    ...ANSI_COLORS,
  },
  light: {
    background: '#e8e8f0',
    foreground: '#1a1a2e',
    cursor: '#3a6a7a',
    selectionBackground: '#3a6a7a44',
    ...ANSI_COLORS,
  },
  midnight: {
    background: '#000000',
    foreground: '#c8c8e0',
    cursor: '#4a7a8a',
    selectionBackground: '#4a7a8a44',
    ...ANSI_COLORS,
  },
  amber: {
    background: '#0a0700',
    foreground: '#ffcc66',
    cursor: '#cc8800',
    selectionBackground: '#cc880044',
    ...ANSI_COLORS,
  },
}

function applyTheme(theme: Theme): void {
  const html = document.documentElement
  for (const t of NON_DARK_THEMES) {
    html.classList.remove(`theme-${t}`)
  }
  if (theme !== 'dark') {
    html.classList.add(`theme-${theme}`)
  }
  const terminal = getTerminal()
  if (terminal) {
    terminal.options.theme = XTERM_THEMES[theme]
  }
}

function storageKey(name: string): string {
  return `tapestry:settings:${name}`
}

interface SettingsState {
  theme: Theme
  characterName: string | null
  settingsOpen: boolean
  setCharacter: (name: string) => void
  setTheme: (theme: Theme) => void
  toggleSettings: () => void
}

export const useSettingsStore = create<SettingsState>()((set, get) => ({
  theme: 'dark',
  characterName: null,
  settingsOpen: false,

  setCharacter: (name) => {
    const raw = localStorage.getItem(storageKey(name))
    const theme: Theme = raw && VALID_THEMES.includes(raw as Theme) ? (raw as Theme) : 'dark'
    set({ characterName: name, theme })
    applyTheme(theme)
  },

  setTheme: (theme) => {
    const { characterName } = get()
    set({ theme })
    if (characterName) {
      localStorage.setItem(storageKey(characterName), theme)
    }
    applyTheme(theme)
  },

  toggleSettings: () => set((s) => ({ settingsOpen: !s.settingsOpen })),
}))
