import { create } from 'zustand'
import type { RoomInfo } from '../types/gmcp'
import type { RoomNode } from '../types/game'
import { useCharStore } from './charStore'

const DIR_OFFSETS: Record<string, { dx: number; dy: number; dz: number }> = {
  north: { dx: 0, dy: 1, dz: 0 },  south: { dx: 0, dy: -1, dz: 0 },
  east:  { dx: 1, dy: 0, dz: 0 },  west:  { dx: -1, dy: 0, dz: 0 },
  up:    { dx: 0, dy: 0, dz: 1 },  down:  { dx: 0, dy: 0, dz: -1 },
}

const DIR_NORMALIZE: Record<string, string> = {
  n: 'north', s: 'south', e: 'east', w: 'west', u: 'up', d: 'down',
}

function normalizeExits(exits: Record<string, string>): Record<string, string> {
  const result: Record<string, string> = {}
  for (const [dir, dest] of Object.entries(exits)) {
    result[DIR_NORMALIZE[dir] ?? dir] = dest
  }
  return result
}

function normalizeDoors(
  doors: Record<string, { isClosed: boolean; isLocked: boolean }> | null | undefined
): Record<string, { isClosed: boolean; isLocked: boolean }> {
  if (!doors) { return {} }
  const result: Record<string, { isClosed: boolean; isLocked: boolean }> = {}
  for (const [dir, state] of Object.entries(doors)) {
    result[DIR_NORMALIZE[dir] ?? dir] = state
  }
  return result
}

function mapStorageKey(charName: string): string {
  return `tapestry:map:${charName}`
}

function saveMapToStorage(charName: string, graph: Map<number, RoomNode>): void {
  if (!charName) { return }
  localStorage.setItem(mapStorageKey(charName), JSON.stringify([...graph.entries()]))
}

interface RoomCurrent {
  num: string
  name: string
  area: string
  environment: string
  description: string
  weatherExposed: boolean
  timeExposed: boolean
  doors: Record<string, { isClosed: boolean; isLocked: boolean }>
  exits: Record<string, string>
}

interface RoomStoreState {
  current: RoomCurrent
  mapGraph: Map<string, RoomNode>
  lastDirection: string | null
  updateRoom: (data: RoomInfo) => void
  setLastDirection: (dir: string | null) => void
  removeExit: (direction: string) => void
  loadMapForCharacter: (charName: string) => void
}

const defaultCurrent: RoomCurrent = {
  num: '', name: '', area: '', environment: '',
  description: '', weatherExposed: false, timeExposed: false, doors: {}, exits: {},
}

export const useRoomStore = create<RoomStoreState>()((set) => ({
  current: defaultCurrent,
  mapGraph: new Map<string, RoomNode>(),
  lastDirection: null,

  updateRoom: (data) =>
    set((s) => {
      const exits = normalizeExits(data.exits)
      const doors = normalizeDoors(data.doors)
      const graph = new Map(s.mapGraph)

      if (!graph.has(data.num)) {
        let x = 0, y = 0, z = 0
        if (s.lastDirection && graph.has(s.current.num)) {
          const prev = graph.get(s.current.num)!
          const off = DIR_OFFSETS[s.lastDirection] ?? { dx: 0, dy: 0, dz: 0 }
          x = prev.x + off.dx; y = prev.y + off.dy; z = prev.z + off.dz
        }
        graph.set(data.num, { num: data.num, name: data.name, x, y, z, exits })
      }

      const charName = useCharStore.getState().name
      saveMapToStorage(charName, graph)

      return {
        current: {
          num: data.num,
          name: data.name,
          area: data.area,
          environment: data.environment,
          description: data.description ?? '',
          weatherExposed: data.weatherExposed ?? false,
          timeExposed: data.timeExposed ?? false,
          doors,
          exits,
        },
        mapGraph: graph,
        lastDirection: null,
      }
    }),

  setLastDirection: (lastDirection) => set({ lastDirection }),

  removeExit: (direction) =>
    set((s) => {
      const exits = { ...s.current.exits }
      delete exits[direction]
      const graph = new Map(s.mapGraph)
      const node = graph.get(s.current.num)
      if (node) {
        const nodeExits = { ...node.exits }
        delete nodeExits[direction]
        graph.set(s.current.num, { ...node, exits: nodeExits })
      }
      return { current: { ...s.current, exits }, mapGraph: graph }
    }),

  loadMapForCharacter: (charName) => {
    if (!charName) { return }
    try {
      const raw = localStorage.getItem(mapStorageKey(charName))
      if (!raw) { return }
      const entries = JSON.parse(raw) as [string, RoomNode][]
      set({ mapGraph: new Map(entries) })
    } catch {
      // corrupt storage -- start fresh
    }
  },
}))
