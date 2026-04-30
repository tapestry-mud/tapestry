import { useRoomStore } from '../stores/roomStore'
import { CompassButtons } from '../controls/CompassButtons'
import type { RoomNode } from '../types/game'

const MAP_RADIUS = 2
const CELL_SIZE = 10

function MiniMap({ graph, currentNum }: { graph: Map<number, RoomNode>; currentNum: number }) {
  const current = graph.get(currentNum)
  if (!current) { return <div className="w-24 h-24 bg-surface rounded" /> }

  const inView = Array.from(graph.values()).filter(
    (r) => Math.abs(r.x - current.x) <= MAP_RADIUS && Math.abs(r.y - current.y) <= MAP_RADIUS && r.z === current.z
  )

  const size = (MAP_RADIUS * 2 + 1) * CELL_SIZE
  const toCell = (r: RoomNode) => ({
    cx: (r.x - current.x + MAP_RADIUS) * CELL_SIZE + CELL_SIZE / 2,
    cy: (MAP_RADIUS - (r.y - current.y)) * CELL_SIZE + CELL_SIZE / 2,
  })

  return (
    <svg width={size} height={size} className="bg-surface rounded border border-border">
      {inView.map((room) => {
        const { cx, cy } = toCell(room)
        const isCurrent = room.num === currentNum
        return (
          <rect
            key={room.num}
            x={cx - 3} y={cy - 3} width={6} height={6}
            fill={isCurrent ? '#5b8a9a' : '#3a3a5c'}
            stroke={isCurrent ? '#55ffff' : '#555577'}
            strokeWidth={1}
          />
        )
      })}
    </svg>
  )
}

export function RoomPanel() {
  const { current, mapGraph } = useRoomStore()

  return (
    <div className="flex flex-col gap-2 p-1">
      <div>
        <div className="text-text-primary text-sm font-bold">{current.name}</div>
        <div className="text-text-secondary text-xs">{current.area}</div>
      </div>
      <div className="flex gap-2 items-center">
        <MiniMap graph={mapGraph} currentNum={current.num} />
        <CompassButtons exits={current.exits} />
      </div>
    </div>
  )
}
