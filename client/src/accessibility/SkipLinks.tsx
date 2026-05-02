import { useEffect, useRef } from 'react'
import { useSettingsStore } from '../stores/settingsStore'
import { useAnnounceStore } from './announceStore'

export function SkipLinks() {
  const openSettings = useSettingsStore((s) => s.openSettings)
  const announced = useRef(false)

  useEffect(() => {
    if (announced.current) { return }
    announced.current = true
    setTimeout(() => {
      useAnnounceStore.getState().pushMessage(
        'Tapestry game loaded. Press F6 to cycle between command input and game panels.',
        'polite'
      )
    }, 1000)
  }, [])

  function handleAnnounceSettings(e: React.MouseEvent | React.KeyboardEvent) {
    e.preventDefault()
    openSettings()
    requestAnimationFrame(() => {
      document.getElementById('announce-settings')?.focus()
    })
  }

  return (
    <nav aria-label="Skip links" className="sr-only focus-within:not-sr-only focus-within:fixed focus-within:top-0 focus-within:left-0 focus-within:z-50 focus-within:flex focus-within:gap-2 focus-within:p-2 focus-within:bg-surface-deep">
      <a
        href="#command-input"
        className="bg-accent text-white px-3 py-1.5 rounded text-sm font-mono focus:outline-none focus:ring-2 focus:ring-accent"
      >
        Skip to command input
      </a>
      <a
        href="#announce-settings"
        onClick={handleAnnounceSettings}
        onKeyDown={(e) => {
          if (e.key === 'Enter') { handleAnnounceSettings(e) }
        }}
        className="bg-accent text-white px-3 py-1.5 rounded text-sm font-mono focus:outline-none focus:ring-2 focus:ring-accent"
      >
        Announcement settings
      </a>
    </nav>
  )
}
