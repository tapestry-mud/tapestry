import { useRoomStore } from '../stores/roomStore'
import { PanelWrapper } from './PanelWrapper'
import { CompassButtons } from '../controls/CompassButtons'

const COLS = 9
const ROWS = 7
const CX = 4
const CY = 3
const ROOM_PX = 22
const CORRIDOR_PX = 9

const ICOLS = COLS * 2 - 1
const IROWS = ROWS * 2 - 1

const GRID_COLS = Array.from({ length: ICOLS }, (_, i) => i % 2 === 0 ? `${ROOM_PX}px` : `${CORRIDOR_PX}px`).join(' ')
const GRID_ROWS = Array.from({ length: IROWS }, (_, i) => i % 2 === 0 ? `${ROOM_PX}px` : `${CORRIDOR_PX}px`).join(' ')

function MiniMap() {
  const current = useRoomStore((s) => s.current)
  const mapGraph = useRoomStore((s) => s.mapGraph)

  const currentNode = mapGraph.get(current.num)
  const originX = currentNode?.x ?? 0
  const originY = currentNode?.y ?? 0
  const originZ = currentNode?.z ?? 0

  // Map grid position "col,row" -> roomId
  const grid = new Map<string, string>()
  for (const room of mapGraph.values()) {
    if (room.z !== originZ) { continue }
    const col = room.x - originX + CX
    const row = originY - room.y + CY
    if (col < 0 || col >= COLS || row < 0 || row >= ROWS) { continue }
    grid.set(`${col},${row}`, room.num)
  }

  const cells: React.ReactNode[] = []

  for (let ir = 0; ir < IROWS; ir++) {
    for (let ic = 0; ic < ICOLS; ic++) {
      const roomCol = ic % 2 === 0
      const roomRow = ir % 2 === 0
      const w = roomCol ? ROOM_PX : CORRIDOR_PX
      const h = roomRow ? ROOM_PX : CORRIDOR_PX

      if (roomCol && roomRow) {
        const col = ic / 2
        const row = ir / 2
        const roomId = grid.get(`${col},${row}`)
        const isYou = roomId === current.num
        const roomName = roomId ? (mapGraph.get(roomId)?.name ?? '') : ''
        cells.push(
          <div key={`${ic},${ir}`} style={{ width: w, height: h }}
            title={roomName || undefined}
            className={roomId
              ? `flex items-center justify-center rounded-sm border ${isYou ? 'border-yellow-400 bg-yellow-400/20' : 'border-text-secondary/40'}`
              : ''}
          >
            {isYou && <span className="text-yellow-400 text-[8px] leading-none">◆</span>}
          </div>
        )
      } else if (!roomCol && roomRow) {
        // Horizontal corridor: between (leftCol, row) and (rightCol, row)
        const leftCol = Math.floor(ic / 2)
        const row = ir / 2
        const leftId = grid.get(`${leftCol},${row}`)
        const rightId = grid.get(`${leftCol + 1},${row}`)
        const leftRoom = leftId ? mapGraph.get(leftId) : null
        const connected = leftRoom && rightId && leftRoom.exits['east'] === rightId
        cells.push(
          <div key={`${ic},${ir}`} style={{ width: w, height: h }} className="flex items-center">
            {connected && <div className="w-full bg-text-secondary/40" style={{ height: 1 }} />}
          </div>
        )
      } else if (roomCol && !roomRow) {
        // Vertical corridor: between (col, topRow) and (col, bottomRow)
        const col = ic / 2
        const topRow = Math.floor(ir / 2)
        const topId = grid.get(`${col},${topRow}`)
        const bottomId = grid.get(`${col},${topRow + 1}`)
        const topRoom = topId ? mapGraph.get(topId) : null
        const connected = topRoom && bottomId && topRoom.exits['south'] === bottomId
        cells.push(
          <div key={`${ic},${ir}`} style={{ width: w, height: h }} className="flex justify-center">
            {connected && <div className="h-full bg-text-secondary/40" style={{ width: 1 }} />}
          </div>
        )
      } else {
        cells.push(<div key={`${ic},${ir}`} style={{ width: w, height: h }} />)
      }
    }
  }

  return (
    <div className="bg-surface border border-border rounded p-1.5 w-fit">
      <div className="grid" style={{ gridTemplateColumns: GRID_COLS, gridTemplateRows: GRID_ROWS }}>
        {cells}
      </div>
    </div>
  )
}

export function MapPanel() {
  const current = useRoomStore((s) => s.current)

  return (
    <PanelWrapper title="MAP">
      <div className="flex items-end justify-between gap-3">
        <MiniMap />
        <CompassButtons exits={current.exits} doors={current.doors} />
      </div>
    </PanelWrapper>
  )
}
