import { useCharStore } from '../stores/charStore'
import { PanelWrapper } from './PanelWrapper'

function StatRow({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex gap-1">
      <span className="text-text-secondary w-5">{label}</span>
      <span className="text-text-primary w-4 text-right">{value > 0 ? value : '-'}</span>
    </div>
  )
}

export function StatsPanel() {
  const str = useCharStore((s) => s.str)
  const int = useCharStore((s) => s.int)
  const wis = useCharStore((s) => s.wis)
  const dex = useCharStore((s) => s.dex)
  const con = useCharStore((s) => s.con)
  const luk = useCharStore((s) => s.luk)

  return (
    <PanelWrapper title="Stats">
      <div className="font-mono text-xs grid grid-cols-3 gap-x-3 gap-y-0.5">
        <StatRow label="STR" value={str} />
        <StatRow label="INT" value={int} />
        <StatRow label="WIS" value={wis} />
        <StatRow label="DEX" value={dex} />
        <StatRow label="CON" value={con} />
        <StatRow label="LUK" value={luk} />
      </div>
    </PanelWrapper>
  )
}
