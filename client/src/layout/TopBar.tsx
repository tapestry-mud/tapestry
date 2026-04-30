import { useRoomStore } from '../stores/roomStore'
import { useSettingsStore } from '../stores/settingsStore'

export function TopBar() {
  const area = useRoomStore((s) => s.current.area)
  const toggleSettings = useSettingsStore((s) => s.toggleSettings)

  return (
    <div className="flex items-center justify-between px-3 py-1 bg-surface-deep border-b border-border font-mono text-sm shrink-0">
      <span className="text-text-primary font-bold tracking-widest">TAPESTRY</span>
      <div className="flex items-center gap-3 text-text-secondary text-xs">
        {area && <span>[{area}]</span>}
        <span>[{'\u{1F441}'} 1,247]</span>
        <button
          title="Open settings"
          onClick={toggleSettings}
          className="text-text-secondary hover:text-text-primary text-base leading-none"
        >
          &#9881;
        </button>
      </div>
    </div>
  )
}
