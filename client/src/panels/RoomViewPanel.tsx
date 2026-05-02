import { useRoomStore } from '../stores/roomStore'
import { useNearbyStore } from '../stores/nearbyStore'
import { useDisplayStore } from '../stores/displayStore'
import { renderTags } from '../utils/renderTags'

export function RoomViewPanel() {
  const current = useRoomStore((s) => s.current)
  const entities = useNearbyStore((s) => s.entities)
  const colorMap = useDisplayStore((s) => s.colorMap)
  const exits = Object.keys(current.exits)
  const exitCount = exits.length

  return (
    <div className="px-4 py-3 font-mono text-sm border-b border-border shrink-0 bg-surface-raised" aria-hidden="true">
      <div className="text-yellow-400 font-bold mb-0.5">{current.name}</div>
      {current.area && (
        <div className="text-text-secondary text-[10px] uppercase tracking-wider mb-2">
          {current.area} · {exitCount} {exitCount === 1 ? 'exit' : 'exits'}
        </div>
      )}
      {current.description && (
        <div className="text-text-secondary text-xs leading-relaxed mb-2">
          {renderTags(current.description, colorMap).map((seg, i) => (
            <span key={i} className={seg.htmlClass}>{seg.text}</span>
          ))}
        </div>
      )}
      {exits.length > 0 && (
        <div className="text-xs mb-2">
          <span className="text-text-secondary">Exits:</span>{' '}
          <span className="text-cyan-500">{exits.join(', ')}</span>
        </div>
      )}
      {entities.map((e, i) => (
        <div key={i} className="text-text-secondary text-xs italic">{e.name} is here.</div>
      ))}
    </div>
  )
}
