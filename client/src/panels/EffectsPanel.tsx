import { useAffectsStore } from '../stores/affectsStore'
import { PanelWrapper } from './PanelWrapper'

export function EffectsPanel() {
  const affects = useAffectsStore((s) => s.affects)

  return (
    <PanelWrapper title="Effects" count={affects.length}>
      <div className="font-mono text-xs">
        {affects.length === 0 ? (
          <div className="text-text-secondary">No active effects</div>
        ) : (
          affects.map((a) => (
            <div key={a.id} className="flex items-center gap-1.5 py-0.5">
              <span className="shrink-0">{a.type === 'buff' ? '\u{1F7E2}' : '\u{1F534}'}</span>
              <span className="flex-1 text-text-primary truncate">{a.name}</span>
              <span className="text-text-secondary shrink-0">
                {a.duration < 0 ? 'perm' : `${a.duration}p`}
              </span>
            </div>
          ))
        )}
      </div>
    </PanelWrapper>
  )
}
