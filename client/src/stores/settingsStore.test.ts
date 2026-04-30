import { useSettingsStore } from './settingsStore'
import { getTerminal } from '../terminal/terminalStore'

vi.mock('../terminal/terminalStore')

const mockTerminal = { options: {} as { theme: unknown } }

beforeEach(() => {
  localStorage.clear()
  document.documentElement.classList.remove('theme-light', 'theme-midnight', 'theme-amber')
  useSettingsStore.setState({ theme: 'dark', characterName: null, settingsOpen: false })
  vi.mocked(getTerminal).mockReturnValue(mockTerminal as any)
  mockTerminal.options = {}
})

describe('defaults', () => {
  it('starts with dark theme and no character', () => {
    const { theme, characterName, settingsOpen } = useSettingsStore.getState()
    expect(theme).toBe('dark')
    expect(characterName).toBeNull()
    expect(settingsOpen).toBe(false)
  })
})

describe('applyTheme via setTheme', () => {
  it('adds theme-light class for light theme', () => {
    useSettingsStore.getState().setTheme('light')
    expect(document.documentElement.classList.contains('theme-light')).toBe(true)
  })

  it('removes prior theme class when switching', () => {
    document.documentElement.classList.add('theme-midnight')
    useSettingsStore.getState().setTheme('light')
    expect(document.documentElement.classList.contains('theme-midnight')).toBe(false)
    expect(document.documentElement.classList.contains('theme-light')).toBe(true)
  })

  it('removes all theme classes for dark', () => {
    document.documentElement.classList.add('theme-amber')
    useSettingsStore.getState().setTheme('dark')
    expect(document.documentElement.classList.contains('theme-amber')).toBe(false)
    expect(document.documentElement.classList.contains('theme-light')).toBe(false)
    expect(document.documentElement.classList.contains('theme-midnight')).toBe(false)
  })

  it('sets xterm theme with correct background for midnight', () => {
    useSettingsStore.getState().setTheme('midnight')
    expect(mockTerminal.options.theme).toMatchObject({ background: '#000000', foreground: '#c8c8e0' })
  })

  it('sets xterm theme with correct background for amber', () => {
    useSettingsStore.getState().setTheme('amber')
    expect(mockTerminal.options.theme).toMatchObject({ background: '#0a0700', foreground: '#ffcc66' })
  })

  it('handles null terminal gracefully', () => {
    vi.mocked(getTerminal).mockReturnValue(null)
    expect(() => useSettingsStore.getState().setTheme('light')).not.toThrow()
  })
})

describe('setCharacter', () => {
  it('defaults to dark when no localStorage entry exists', () => {
    useSettingsStore.getState().setCharacter('Aragorn')
    expect(useSettingsStore.getState().theme).toBe('dark')
  })

  it('loads saved theme from localStorage', () => {
    localStorage.setItem('tapestry:settings:Aragorn', 'amber')
    useSettingsStore.getState().setCharacter('Aragorn')
    expect(useSettingsStore.getState().theme).toBe('amber')
    expect(document.documentElement.classList.contains('theme-amber')).toBe(true)
  })

  it('ignores invalid localStorage values and falls back to dark', () => {
    localStorage.setItem('tapestry:settings:Aragorn', 'neon-pink')
    useSettingsStore.getState().setCharacter('Aragorn')
    expect(useSettingsStore.getState().theme).toBe('dark')
  })

  it('sets characterName in state', () => {
    useSettingsStore.getState().setCharacter('Aragorn')
    expect(useSettingsStore.getState().characterName).toBe('Aragorn')
  })
})

describe('setTheme persistence', () => {
  it('saves to localStorage keyed by characterName when set', () => {
    useSettingsStore.setState({ characterName: 'Aragorn' })
    useSettingsStore.getState().setTheme('midnight')
    expect(localStorage.getItem('tapestry:settings:Aragorn')).toBe('midnight')
  })

  it('does not write localStorage when characterName is null', () => {
    useSettingsStore.getState().setTheme('midnight')
    expect(localStorage.length).toBe(0)
  })
})

describe('toggleSettings', () => {
  it('flips settingsOpen from false to true', () => {
    useSettingsStore.getState().toggleSettings()
    expect(useSettingsStore.getState().settingsOpen).toBe(true)
  })

  it('flips settingsOpen from true to false', () => {
    useSettingsStore.setState({ settingsOpen: true })
    useSettingsStore.getState().toggleSettings()
    expect(useSettingsStore.getState().settingsOpen).toBe(false)
  })
})
