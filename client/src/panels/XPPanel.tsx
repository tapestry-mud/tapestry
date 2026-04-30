import { useXpStore } from '../stores/xpStore'
import type { XpTrack } from '../stores/xpStore'
import { PanelWrapper } from './PanelWrapper'

function calcPct(track: XpTrack): number {
  if (track.xpToNext <= 0) { return 100 }
  const progressInLevel = track.xp - track.currentLevelThreshold
  const levelRange = track.xpToNext + progressInLevel
  if (levelRange <= 0) { return 0 }
  return Math.floor((progressInLevel / levelRange) * 100)
}

export function XPPanel() {
  const tracks = useXpStore((s) => s.tracks)

  if (tracks.length === 0) {
    return (
      <PanelWrapper title="Experience">
        <div className="font-mono text-xs text-text-secondary">No progression data</div>
      </PanelWrapper>
    )
  }

  return (
    <PanelWrapper title="Experience">
      <div className="font-mono text-xs space-y-1.5">
        {tracks.map((track) => {
          const pct = calcPct(track)
          const trackName = track.name.charAt(0).toUpperCase() + track.name.slice(1)
          return (
            <div key={track.name}>
              <div className="flex justify-between text-text-secondary mb-0.5">
                <span>{trackName}</span>
                <span>Lvl {track.level}</span>
              </div>
              <div className="flex items-center gap-2 mb-0.5">
                <div className="flex-1 h-2.5 bg-border rounded-sm overflow-hidden min-w-0">
                  <div
                    data-bar={`xp-${track.name}`}
                    className="h-full transition-all duration-300"
                    style={{ width: `${pct}%`, background: 'linear-gradient(90deg, var(--color-accent), oklch(0.78 0.12 75))' }}
                  />
                </div>
                <span className="text-text-primary shrink-0">{pct}%</span>
              </div>
              <div className="text-text-secondary">
                {track.xp.toLocaleString()} / {(track.xp + track.xpToNext).toLocaleString()}
              </div>
            </div>
          )
        })}
      </div>
    </PanelWrapper>
  )
}
