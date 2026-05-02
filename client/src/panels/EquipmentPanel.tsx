import { useEquipmentStore } from '../stores/equipmentStore'
import { useDisplayStore } from '../stores/displayStore'
import { PanelWrapper } from './PanelWrapper'
import { renderTags } from '../utils/renderTags'

const SLOT_DISPLAY_ORDER = [
  'light', 'head', 'neck', 'body', 'torso', 'cloak', 'waist',
  'arms', 'hands',
  'wrist:0', 'wrist:1', 'wrist',
  'finger:0', 'finger:1', 'finger',
  'shield', 'legs', 'feet', 'held', 'floating', 'wield',
]

function slotLabel(slotKey: string): string {
  const base = slotKey.includes(':') ? slotKey.slice(0, slotKey.indexOf(':')) : slotKey
  return base.charAt(0).toUpperCase() + base.slice(1)
}

export function EquipmentPanel() {
  const slots = useEquipmentStore((s) => s.slots)
  const colorMap = useDisplayStore((s) => s.colorMap)
  const allKeys = Object.keys(slots)
  const sortedKeys = [
    ...SLOT_DISPLAY_ORDER.filter((k) => allKeys.includes(k)),
    ...allKeys.filter((k) => !SLOT_DISPLAY_ORDER.includes(k)).sort(),
  ]
  if (sortedKeys.length === 0) {
    return (
      <PanelWrapper title="Equipment" defaultCollapsed>
        <div className="text-text-secondary text-xs italic">Nothing worn.</div>
      </PanelWrapper>
    )
  }

  return (
    <PanelWrapper title="Equipment" defaultCollapsed>
      <div className="font-mono text-xs">
        {sortedKeys.map((key) => {
          const item = slots[key]
          const raritySegs = item?.rarity ? renderTags(item.rarity, colorMap) : []
          const essenceSegs = item?.essence ? renderTags(item.essence, colorMap) : []
          return (
            <div key={key} className="flex items-baseline py-0.5 gap-1">
              <span className="text-text-secondary w-20 shrink-0 text-right">[{slotLabel(key)}]</span>
              {item ? (
                <>
                  {raritySegs.length > 0 && (
                    <span className="shrink-0">
                      {raritySegs.map((seg, i) => (
                        <span key={i} className={seg.htmlClass}>{seg.text}</span>
                      ))}
                    </span>
                  )}
                  <span className="text-text-primary">{item.name}</span>
                  {essenceSegs.length > 0 && (
                    <span className="shrink-0">
                      {essenceSegs.map((seg, i) => (
                        <span key={i} className={seg.htmlClass}>{seg.text}</span>
                      ))}
                    </span>
                  )}
                </>
              ) : (
                <span className="text-text-secondary italic">-nothing-</span>
              )}
            </div>
          )
        })}
      </div>
    </PanelWrapper>
  )
}
