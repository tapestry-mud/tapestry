import { WebSocketClient } from '../connection/WebSocketClient'

interface DoorInfo {
  isClosed: boolean
  isLocked: boolean
}

interface CompassButtonsProps {
  exits: Record<string, string>
  doors?: Record<string, DoorInfo>
}

type ExitState = 'none' | 'open' | 'door'

interface DirButtonProps {
  dir: string
  label: string
  state: ExitState
}

function DirButton({ dir, label, state }: DirButtonProps) {
  const available = state !== 'none'
  return (
    <button
      title={dir}
      disabled={!available}
      onClick={() => { if (available) { WebSocketClient.send(dir) } }}
      className={`w-7 h-7 text-xs rounded border flex items-center justify-center transition-colors
        ${state === 'none'
          ? 'border-border text-border cursor-not-allowed opacity-40'
          : state === 'door'
          ? 'border-rose-500 text-rose-300 hover:bg-rose-500 hover:text-white cursor-pointer'
          : 'border-accent text-accent hover:bg-accent hover:text-white cursor-pointer'
        }`}
    >
      {label}
    </button>
  )
}

export function CompassButtons({ exits, doors = {} }: CompassButtonsProps) {
  const exitState = (dir: string): ExitState => {
    if (!(dir in exits)) { return 'none' }
    if (dir in doors) { return 'door' }
    return 'open'
  }

  return (
    <div className="flex gap-1 items-center">
      <div className="grid grid-cols-3 grid-rows-3 gap-0.5">
        <div />
        <DirButton dir="north" label="N" state={exitState('north')} />
        <div />
        <DirButton dir="west"  label="W" state={exitState('west')} />
        <div className="w-7 h-7 flex items-center justify-center text-text-secondary text-xs">+</div>
        <DirButton dir="east"  label="E" state={exitState('east')} />
        <div />
        <DirButton dir="south" label="S" state={exitState('south')} />
        <div />
      </div>
      <div className="flex flex-col gap-0.5">
        <DirButton dir="up"   label="U" state={exitState('up')} />
        <DirButton dir="down" label="D" state={exitState('down')} />
      </div>
    </div>
  )
}
