import { useCombatTargetsStore } from '../stores/combatTargetsStore'
import { PanelWrapper } from './PanelWrapper'

const TIER_COLORS: Record<string, string> = {
  'perfect': 'bg-green-500',
  'few scratches': 'bg-green-400',
  'small wounds': 'bg-yellow-400',
  'wounded': 'bg-yellow-500',
  'badly wounded': 'bg-orange-500',
  'bleeding profusely': 'bg-red-500',
  'near death': 'bg-red-700',
}

const TIER_WIDTH: Record<string, string> = {
  'perfect': 'w-full',
  'few scratches': 'w-5/6',
  'small wounds': 'w-2/3',
  'wounded': 'w-1/2',
  'badly wounded': 'w-1/3',
  'bleeding profusely': 'w-1/6',
  'near death': 'w-[5%]',
}

export function CombatTargetPanel() {
  const targets = useCombatTargetsStore((s) => s.targets)

  if (targets.length === 0) { return null }

  return (
    <PanelWrapper title="Combat" count={targets.length}>
      <div className="font-mono text-xs space-y-1.5">
        {targets.map((target) => {
          const barColor = TIER_COLORS[target.healthTier] ?? 'bg-gray-500'
          const barWidth = TIER_WIDTH[target.healthTier] ?? 'w-1/2'
          return (
            <div key={target.id}>
              <div className={`text-text-primary truncate ${target.isPrimary ? 'font-bold' : 'opacity-75'}`}>
                {target.isPrimary ? '> ' : '  '}{target.name}
              </div>
              <div className="mt-0.5 h-1.5 bg-surface-deep rounded overflow-hidden">
                <div className={`h-full ${barColor} ${barWidth} rounded transition-all duration-300`} />
              </div>
            </div>
          )
        })}
      </div>
    </PanelWrapper>
  )
}
