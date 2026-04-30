import { useAffectsStore } from '../stores/affectsStore'
import { PanelWrapper } from './PanelWrapper'

export function AffectsPanel() {
  const { affects } = useAffectsStore()

  return (
    <PanelWrapper title="Affects" count={affects.length}>
      {affects.length === 0 ? (
        <p className="text-text-secondary text-xs italic">No active affects</p>
      ) : (
        <ul className="flex flex-col gap-0.5">
          {affects.map((a) => (
            <li key={a.name} className="flex items-center gap-2 text-xs">
              <span className={a.type === 'debuff' ? 'text-ansi-red' : 'text-ansi-green'}>{a.name}</span>
              {a.duration > 0 && <span className="text-text-secondary ml-auto">{a.duration}s</span>}
            </li>
          ))}
        </ul>
      )}
    </PanelWrapper>
  )
}
