import { beforeEach, describe, it, expect } from 'vitest'
import { useRoomStore } from './roomStore'
import { useCharStore } from './charStore'

const nullRoom = { num: '', name: '', area: '', environment: '', exits: {}, description: '', weatherExposed: false, timeExposed: false, doors: {} }

beforeEach(() => { useRoomStore.setState({ current: nullRoom, mapGraph: new Map(), lastDirection: null }) })

beforeEach(() => {
  useCharStore.setState({ name: '', race: '', class: '', level: 0, hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0 })
  localStorage.clear()
})

describe('roomStore', () => {
  it('updateRoom sets current room fields', () => {
    useRoomStore.getState().updateRoom({ num: 'core:town-square', name: 'Town Square', area: 'Midgaard', environment: 'city', exits: { north: 'core:inn' } })
    expect(useRoomStore.getState().current.name).toBe('Town Square')
    expect(useRoomStore.getState().current.exits.north).toBe('core:inn')
  })

  it('first room added to mapGraph at 0,0,0', () => {
    useRoomStore.getState().updateRoom({ num: 'core:town-square', name: 'Town Square', area: 'Midgaard', environment: 'city', exits: {} })
    const node = useRoomStore.getState().mapGraph.get('core:town-square')
    expect(node?.x).toBe(0)
    expect(node?.y).toBe(0)
    expect(node?.z).toBe(0)
  })

  it('north move increments y by 1', () => {
    useRoomStore.getState().updateRoom({ num: 'core:start', name: 'Start', area: 'A', environment: 'o', exits: { north: 'core:north' } })
    useRoomStore.getState().setLastDirection('north')
    useRoomStore.getState().updateRoom({ num: 'core:north', name: 'North Room', area: 'A', environment: 'o', exits: {} })
    const node = useRoomStore.getState().mapGraph.get('core:north')
    expect(node?.y).toBe(1)
  })

  it('setLastDirection stores direction', () => {
    useRoomStore.getState().setLastDirection('north')
    expect(useRoomStore.getState().lastDirection).toBe('north')
  })

  it('normalizes short exit keys from server', () => {
    useRoomStore.getState().updateRoom({ num: 'core:town-square', name: 'Town Square', area: 'Midgaard', environment: 'city', exits: { n: 'core:inn', e: 'core:store' } })
    const exits = useRoomStore.getState().current.exits
    expect(exits.north).toBe('core:inn')
    expect(exits.east).toBe('core:store')
    expect(exits.n).toBeUndefined()
    expect(exits.e).toBeUndefined()
  })
})

describe('roomStore -- new fields', () => {
  it('updateRoom sets description, weatherExposed, timeExposed, doors', () => {
    useRoomStore.getState().updateRoom({
      num: 'core:cave', name: 'Cave', area: 'Wilds', environment: 'cave',
      exits: {}, description: 'Damp.', weatherExposed: false, timeExposed: true,
      doors: { north: { isClosed: true, isLocked: false } },
    })
    const c = useRoomStore.getState().current
    expect(c.description).toBe('Damp.')
    expect(c.weatherExposed).toBe(false)
    expect(c.timeExposed).toBe(true)
    expect(c.doors.north).toEqual({ isClosed: true, isLocked: false })
  })

  it('updateRoom defaults missing optional fields', () => {
    useRoomStore.getState().updateRoom({
      num: 'core:room', name: 'Room', area: 'A', environment: 'o', exits: {},
    })
    const c = useRoomStore.getState().current
    expect(c.description).toBe('')
    expect(c.weatherExposed).toBe(false)
    expect(c.doors).toEqual({})
  })
})

describe('roomStore -- removeExit', () => {
  it('removes exit from current.exits', () => {
    useRoomStore.getState().updateRoom({
      num: 'core:room', name: 'R', area: 'A', environment: 'o', exits: { north: 'core:north', east: 'core:east' },
    })
    useRoomStore.getState().removeExit('north')
    expect(useRoomStore.getState().current.exits.north).toBeUndefined()
    expect(useRoomStore.getState().current.exits.east).toBe('core:east')
  })

  it('removes exit from mapGraph node', () => {
    useRoomStore.getState().updateRoom({
      num: 'core:room', name: 'R', area: 'A', environment: 'o', exits: { north: 'core:north' },
    })
    useRoomStore.getState().removeExit('north')
    const node = useRoomStore.getState().mapGraph.get('core:room')
    expect(node?.exits.north).toBeUndefined()
  })
})

describe('roomStore -- map persistence', () => {
  it('loadMapForCharacter restores mapGraph from localStorage', () => {
    const node = { num: 'core:saved', name: 'Saved Room', x: 3, y: 1, z: 0, exits: {} }
    localStorage.setItem('tapestry:map:Raegar', JSON.stringify([['core:saved', node]]))
    useRoomStore.getState().loadMapForCharacter('Raegar')
    expect(useRoomStore.getState().mapGraph.get('core:saved')?.name).toBe('Saved Room')
  })

  it('updateRoom saves mapGraph to localStorage when charName known', () => {
    useCharStore.setState({ name: 'Raegar' } as Parameters<typeof useCharStore.setState>[0])
    useRoomStore.getState().updateRoom({
      num: 'core:new-room', name: 'New Room', area: 'A', environment: 'o', exits: {},
    })
    const raw = localStorage.getItem('tapestry:map:Raegar')
    expect(raw).not.toBeNull()
    const entries = JSON.parse(raw!) as [string, object][]
    expect(entries.some(([k]) => k === 'core:new-room')).toBe(true)
  })
})
