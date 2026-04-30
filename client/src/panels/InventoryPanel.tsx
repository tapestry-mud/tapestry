import { useInventoryStore } from '../stores/inventoryStore'
import { useDisplayStore } from '../stores/displayStore'
import { PanelWrapper } from './PanelWrapper'
import { renderTags } from '../utils/renderTags'

export function InventoryPanel() {
  const items = useInventoryStore((s) => s.items)
  const colorMap = useDisplayStore((s) => s.colorMap)
  const total = items.reduce((sum, i) => sum + i.quantity, 0)

  return (
    <PanelWrapper title="Inventory" count={total}>
      {items.length === 0 ? (
        <div className="text-text-secondary text-xs italic">You are carrying nothing.</div>
      ) : (
        <ul className="flex flex-col gap-0.5">
          {items.map((item) => {
            const raritySegs = item.rarityTag ? renderTags(item.rarityTag, colorMap) : []
            const essenceSegs = item.essenceTag ? renderTags(item.essenceTag, colorMap) : []
            return (
              <li key={item.id} className="flex items-baseline gap-1 text-xs font-mono">
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
                {item.quantity > 1 && (
                  <span className="text-text-secondary shrink-0">(x{item.quantity})</span>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </PanelWrapper>
  )
}
