import { useCharStore } from '../stores/charStore'
import { useRoomStore } from '../stores/roomStore'

interface VitalBarProps {
  value: number
  max: number
  colorClass: string
  label: string
  dataBar: string
}

function VitalBar({ value, max, colorClass, label, dataBar }: VitalBarProps) {
  const pct = max > 0 ? Math.round((value / max) * 100) : 0
  return (
    <div aria-label={label} className="relative h-5 w-24 bg-surface rounded overflow-hidden border border-border">
      <div
        data-bar={dataBar}
        className={`absolute inset-y-0 left-0 ${colorClass} transition-all`}
        style={{ width: `${pct}%` }}
      />
      <span className="absolute inset-0 flex items-center justify-center text-xs text-white font-mono leading-none z-10">
        {value}/{max}
      </span>
    </div>
  )
}

export function VitalsBar() {
  const { name, class: cls, level, hp, maxHp, mana, maxMana, mv, maxMv } = useCharStore()
  const { current } = useRoomStore()

  return (
    <div className="flex items-center gap-3 px-3 h-10 bg-surface-raised border-b border-border shrink-0 font-ui text-sm">
      <span className="text-text-primary font-bold">{name}</span>
      <span className="text-text-secondary">{cls} {level}</span>
      <div className="flex-1" />
      <VitalBar value={hp} max={maxHp} colorClass="bg-vital-hp" label="HP" dataBar="hp" />
      <VitalBar value={mana} max={maxMana} colorClass="bg-vital-mana" label="MP" dataBar="mana" />
      <VitalBar value={mv} max={maxMv} colorClass="bg-vital-mv" label="MV" dataBar="mv" />
      <div className="flex-1" />
      <span className="text-text-primary">{current.name}</span>
      <span className="text-text-secondary text-xs">{current.area}</span>
    </div>
  )
}
