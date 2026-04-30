import { cn } from '../lib/utils'
import { useSettingsStore, type Theme } from '../stores/settingsStore'

const THEME_SWATCHES: Record<Theme, { bg: string; accent: string; text: string; border: string }> = {
  dark:     { bg: '#1a1a2e', accent: '#5b8a9a', text: '#e0e0e0', border: '#3a3a5c' },
  light:    { bg: '#f4f4fb', accent: '#3a6a7a', text: '#1a1a2e', border: '#c0c0d8' },
  midnight: { bg: '#080810', accent: '#4a7a8a', text: '#c8c8e0', border: '#252540' },
  amber:    { bg: '#1a1400', accent: '#cc8800', text: '#ffcc66', border: '#4a3800' },
}

const THEME_LABELS: Record<Theme, string> = {
  dark: 'Dark',
  light: 'Light',
  midnight: 'Midnight',
  amber: 'Amber',
}

const THEMES: Theme[] = ['dark', 'light', 'midnight', 'amber']

export function SettingsModal() {
  const settingsOpen = useSettingsStore((s) => s.settingsOpen)
  const activeTheme = useSettingsStore((s) => s.theme)
  const toggleSettings = useSettingsStore((s) => s.toggleSettings)
  const setTheme = useSettingsStore((s) => s.setTheme)

  if (!settingsOpen) {
    return null
  }

  return (
    <div
      data-testid="settings-backdrop"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
      onClick={toggleSettings}
    >
      <div
        className="bg-surface-raised border border-border rounded-lg p-6 w-80 shadow-xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-text-primary font-bold font-mono text-sm">Settings</h2>
          <button
            title="Close settings"
            onClick={toggleSettings}
            className="text-text-secondary hover:text-text-primary text-lg leading-none"
          >
            x
          </button>
        </div>

        <div className="grid grid-cols-2 gap-3">
          {THEMES.map((theme) => {
            const swatch = THEME_SWATCHES[theme]
            const isActive = activeTheme === theme
            return (
              <button
                key={theme}
                onClick={() => setTheme(theme)}
                className={cn(
                  'rounded-lg p-3 border-2 text-left transition-colors',
                  isActive ? 'border-accent' : 'border-border hover:border-text-secondary'
                )}
                style={{ backgroundColor: swatch.bg }}
              >
                <div className="flex gap-1 mb-2">
                  <div className="w-4 h-4 rounded-sm" style={{ backgroundColor: swatch.bg }} />
                  <div className="w-4 h-4 rounded-sm" style={{ backgroundColor: swatch.accent }} />
                  <div className="w-4 h-4 rounded-sm" style={{ backgroundColor: swatch.text }} />
                  <div className="w-4 h-4 rounded-sm" style={{ backgroundColor: swatch.border }} />
                </div>
                <span className="text-xs font-mono" style={{ color: swatch.text }}>
                  {THEME_LABELS[theme]}
                </span>
              </button>
            )
          })}
        </div>
      </div>
    </div>
  )
}
