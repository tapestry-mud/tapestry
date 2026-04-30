import { useWorldStore } from '../stores/worldStore'
import { useRoomStore } from '../stores/roomStore'
import { PanelWrapper } from './PanelWrapper'

const PERIOD_ICON: Record<string, string> = {
  dawn:  '🌅',
  day:   '☀️',
  dusk:  '🌆',
  night: '🌙',
}

const WEATHER_ICON: Record<string, string> = {
  clear:  '☀️',
  cloudy: '⛅',
  rain:   '🌧️',
  storm:  '⛈️',
  snow:   '❄️',
}

function capitalize(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1)
}

export function TimeWeatherPanel() {
  const { hour, period, weatherState } = useWorldStore()
  const { timeExposed, weatherExposed } = useRoomStore((s) => s.current)

  return (
    <PanelWrapper title="WORLD">
      <div className="font-mono text-xs space-y-1.5">
        <div className="flex items-center gap-2">
          {timeExposed && period ? (
            <>
              <span>{PERIOD_ICON[period] ?? '?'}</span>
              <span className="text-text-primary">
                {capitalize(period)} · {hour.toString().padStart(2, '0')}:00
              </span>
            </>
          ) : (
            <span className="text-text-secondary opacity-50">Sheltered</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {weatherExposed && weatherState ? (
            <>
              <span>{WEATHER_ICON[weatherState] ?? '?'}</span>
              <span className="text-text-primary">{capitalize(weatherState)}</span>
            </>
          ) : (
            <span className="text-text-secondary opacity-50">Indoors</span>
          )}
        </div>
      </div>
    </PanelWrapper>
  )
}
