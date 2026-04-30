import { beforeEach, describe, it, expect } from 'vitest'
import { useHotbarStore } from './hotbarStore'

beforeEach(() => { useHotbarStore.setState({ slots: Array(10).fill(null) }) })

describe('hotbarStore', () => {
  it('starts with 10 null slots', () => {
    const { slots } = useHotbarStore.getState()
    expect(slots).toHaveLength(10)
    expect(slots[0]).toBeNull()
  })

  it('setSlot stores config at index', () => {
    useHotbarStore.getState().setSlot(0, { emoji: '⚔️', label: 'atk', command: 'attack' })
    expect(useHotbarStore.getState().slots[0]).toEqual({ emoji: '⚔️', label: 'atk', command: 'attack' })
  })

  it('clearSlot resets index to null', () => {
    useHotbarStore.getState().setSlot(2, { emoji: '🛡️', label: 'def', command: 'defend' })
    useHotbarStore.getState().clearSlot(2)
    expect(useHotbarStore.getState().slots[2]).toBeNull()
  })

  it('ignores out-of-range index', () => {
    useHotbarStore.getState().setSlot(99, { emoji: '?', label: 'bad', command: 'noop' })
    expect(useHotbarStore.getState().slots).toHaveLength(10)
  })
})
