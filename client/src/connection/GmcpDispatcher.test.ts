import { describe, it, expect, vi, beforeEach } from 'vitest'
import { GmcpDispatcher, initCoreHandlers } from './GmcpDispatcher'
import { useConnectionStore } from '../stores/connectionStore'

beforeEach(() => {
  GmcpDispatcher.clear()
})

describe('GmcpDispatcher', () => {
  it('calls registered handler for known package', () => {
    const handler = vi.fn()
    GmcpDispatcher.register('Test.Package', handler)
    GmcpDispatcher.dispatch('Test.Package', { value: 1 })
    expect(handler).toHaveBeenCalledWith({ value: 1 })
  })

  it('does not throw for unrecognized package', () => {
    expect(() => GmcpDispatcher.dispatch('Unknown.Package', {})).not.toThrow()
  })

  it('overwrites handler on re-register', () => {
    const first = vi.fn()
    const second = vi.fn()
    GmcpDispatcher.register('Test.Override', first)
    GmcpDispatcher.register('Test.Override', second)
    GmcpDispatcher.dispatch('Test.Override', {})
    expect(second).toHaveBeenCalled()
    expect(first).not.toHaveBeenCalled()
  })

  it('dispatches to multiple different packages independently', () => {
    const h1 = vi.fn()
    const h2 = vi.fn()
    GmcpDispatcher.register('Pkg.A', h1)
    GmcpDispatcher.register('Pkg.B', h2)
    GmcpDispatcher.dispatch('Pkg.A', 'a')
    expect(h1).toHaveBeenCalledWith('a')
    expect(h2).not.toHaveBeenCalled()
  })
})

describe('Char.Login.Phase handler', () => {
  beforeEach(() => {
    GmcpDispatcher.clear()
    useConnectionStore.setState({ loginPhase: 'disconnected' } as Parameters<typeof useConnectionStore.setState>[0])
    initCoreHandlers()
  })

  it('updates loginPhase on valid phase packet', () => {
    GmcpDispatcher.dispatch('Char.Login.Phase', { phase: 'password' })
    expect(useConnectionStore.getState().loginPhase).toBe('password')
  })

  it('ignores invalid phase packet without throwing', () => {
    const act = () => { GmcpDispatcher.dispatch('Char.Login.Phase', { phase: 'bogus' }) }
    expect(act).not.toThrow()
    expect(useConnectionStore.getState().loginPhase).toBe('disconnected')
  })
})

import { useNearbyStore } from '../stores/nearbyStore'
import { useWorldStore } from '../stores/worldStore'
import { useRoomStore } from '../stores/roomStore'

describe('Room.Nearby handler', () => {
  beforeEach(() => {
    GmcpDispatcher.clear()
    useNearbyStore.setState({ entities: [] })
    initCoreHandlers()
  })

  it('sets entities from valid payload', () => {
    GmcpDispatcher.dispatch('Room.Nearby', {
      entities: [{ name: 'Goblin', type: 'mob', templateId: 'goblin_basic' }],
    })
    expect(useNearbyStore.getState().entities[0].name).toBe('Goblin')
  })

  it('ignores invalid payload without throwing', () => {
    const act = () => { GmcpDispatcher.dispatch('Room.Nearby', { wrong: true }) }
    expect(act).not.toThrow()
    expect(useNearbyStore.getState().entities).toHaveLength(0)
  })
})

describe('World.Time handler', () => {
  beforeEach(() => {
    GmcpDispatcher.clear()
    useWorldStore.setState({ hour: 0, period: null, dayCount: 0, weatherState: null })
    initCoreHandlers()
  })

  it('sets time fields', () => {
    GmcpDispatcher.dispatch('World.Time', { hour: 6, period: 'dawn', dayCount: 10 })
    expect(useWorldStore.getState().period).toBe('dawn')
    expect(useWorldStore.getState().hour).toBe(6)
  })
})

describe('World.Weather handler', () => {
  beforeEach(() => {
    GmcpDispatcher.clear()
    useWorldStore.setState({ hour: 0, period: null, dayCount: 0, weatherState: null })
    initCoreHandlers()
  })

  it('sets weatherState', () => {
    GmcpDispatcher.dispatch('World.Weather', { state: 'rain' })
    expect(useWorldStore.getState().weatherState).toBe('rain')
  })
})

describe('Room.WrongDir handler -- removeExit', () => {
  beforeEach(() => {
    GmcpDispatcher.clear()
    useRoomStore.setState({
      current: {
        num: 1, name: 'R', area: 'A', environment: 'o',
        description: '', weatherExposed: false, timeExposed: false, doors: {},
        exits: { north: 2 },
      },
      mapGraph: new Map([[1, { num: 1, name: 'R', x: 0, y: 0, z: 0, exits: { north: 2 } }]]),
      lastDirection: 'north',
    })
    initCoreHandlers()
  })

  it('removes lastDirection exit from store', () => {
    GmcpDispatcher.dispatch('Room.WrongDir', '')
    expect(useRoomStore.getState().current.exits.north).toBeUndefined()
  })
})
