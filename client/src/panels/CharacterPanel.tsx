import { useEffect } from 'react'
import { useCharStore } from '../stores/charStore'
import { useSettingsStore } from '../stores/settingsStore'
import { PanelWrapper } from './PanelWrapper'

export function CharacterPanel() {
  const name           = useCharStore((s) => s.name)
  const race           = useCharStore((s) => s.race)
  const charClass      = useCharStore((s) => s.class)
  const level          = useCharStore((s) => s.level)
  const alignmentBucket = useCharStore((s) => s.alignmentBucket)
  const gold           = useCharStore((s) => s.gold)
  const hungerTier     = useCharStore((s) => s.hungerTier)

  useEffect(() => {
    if (name) {
      useSettingsStore.getState().setCharacter(name)
    }
  }, [name])

  const hasIdentity = race && race !== 'Unknown' && charClass && charClass !== 'Unknown'

  return (
    <PanelWrapper title="Character">
      <div className="font-mono text-sm space-y-1">
        <div className="text-text-primary font-bold tracking-wide text-base leading-tight">
          {name || 'Unknown'}
        </div>

        {hasIdentity ? (
          <div className="text-text-secondary text-xs">
            {race} {charClass}
          </div>
        ) : null}

        <div className="flex items-center gap-2 text-xs">
          {level > 0 && (
            <span className="bg-surface-overlay border border-border rounded px-1.5 py-0.5 text-text-primary">
              Lvl {level}
            </span>
          )}
          {alignmentBucket && (
            <span className="text-text-secondary capitalize">{alignmentBucket}</span>
          )}
          {hungerTier && hungerTier !== 'full' && (
            <span className="text-ansi-yellow capitalize">{hungerTier}</span>
          )}
        </div>

        {gold > 0 && (
          <div className="flex items-center gap-1 text-xs text-text-secondary">
            <span className="text-ansi-yellow">&#9679;</span>
            <span>{gold.toLocaleString()} gold</span>
          </div>
        )}
      </div>
    </PanelWrapper>
  )
}
