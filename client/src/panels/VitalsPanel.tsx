import { useCharStore } from '../stores/charStore'
import { PanelWrapper } from './PanelWrapper'

interface VitalRowProps {
  icon: string
  current: number
  max: number
  barGradient: string
  barKey: string
}

function VitalRow({ icon, current, max, barGradient, barKey }: VitalRowProps) {
  const pct = max > 0 ? Math.min(100, Math.round((current / max) * 100)) : 0

  return (
    <div className="flex items-center gap-2 font-mono text-xs">
      <span className="w-4 text-center shrink-0">{icon}</span>
      <span className="w-20 text-text-primary text-right shrink-0">
        {current} / {max}
      </span>
      <div className="flex-1 h-2.5 bg-border rounded-sm overflow-hidden min-w-0">
        <div
          data-bar={barKey}
          className="h-full transition-all duration-300"
          style={{ width: `${pct}%`, background: barGradient }}
        />
      </div>
    </div>
  )
}

export function VitalsPanel() {
  const hp = useCharStore((s) => s.hp)
  const maxHp = useCharStore((s) => s.maxHp)
  const mana = useCharStore((s) => s.mana)
  const maxMana = useCharStore((s) => s.maxMana)
  const mv = useCharStore((s) => s.mv)
  const maxMv = useCharStore((s) => s.maxMv)

  return (
    <PanelWrapper title="Vitals">
      <div className="flex flex-col gap-1.5">
        <VitalRow icon="❤" current={hp} max={maxHp} barKey="hp"
          barGradient="linear-gradient(90deg, oklch(0.55 0.16 25), oklch(0.65 0.18 25))" />
        <VitalRow icon="✦" current={mana} max={maxMana} barKey="mana"
          barGradient="linear-gradient(90deg, oklch(0.5 0.14 250), oklch(0.62 0.16 250))" />
        <VitalRow icon="⚡" current={mv} max={maxMv} barKey="mv"
          barGradient="linear-gradient(90deg, oklch(0.55 0.14 145), oklch(0.7 0.16 145))" />
      </div>
    </PanelWrapper>
  )
}
