import { beforeEach, describe, it, expect, vi } from 'vitest'
import { useShortcutStore } from './shortcutStore'

beforeEach(() => {
  useShortcutStore.setState({ shortcuts: new Map() })
  localStorage.clear()
})

describe('shortcutStore', () => {
  it('register adds entry with default key when no saved binding', () => {
    const handler = vi.fn()
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', handler)
    const entry = useShortcutStore.getState().shortcuts.get('room-description')
    expect(entry?.key).toBe('Alt+L')
    expect(entry?.label).toBe('Room description')
    expect(entry?.enabled).toBe(true)
  })

  it('register picks up saved binding over default key', () => {
    localStorage.setItem('tapestry:shortcuts', JSON.stringify({ 'room-description': 'Alt+R' }))
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', vi.fn())
    expect(useShortcutStore.getState().shortcuts.get('room-description')?.key).toBe('Alt+R')
  })

  it('re-registration updates handler but preserves existing key', () => {
    const first = vi.fn()
    const second = vi.fn()
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', first)
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', second)
    const entry = useShortcutStore.getState().shortcuts.get('room-description')
    expect(entry?.key).toBe('Alt+L')
    entry?.handler()
    expect(second).toHaveBeenCalledOnce()
    expect(first).not.toHaveBeenCalled()
  })

  it('getByKey finds enabled entry', () => {
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', vi.fn())
    const entry = useShortcutStore.getState().getByKey('Alt+L')
    expect(entry?.id).toBe('room-description')
  })

  it('getByKey returns undefined for unknown key', () => {
    expect(useShortcutStore.getState().getByKey('Alt+Z')).toBeUndefined()
  })

  it('rebind changes the key in store', () => {
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', vi.fn())
    useShortcutStore.getState().rebind('room-description', 'Alt+R')
    expect(useShortcutStore.getState().shortcuts.get('room-description')?.key).toBe('Alt+R')
  })

  it('rebind persists new key to localStorage', () => {
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', vi.fn())
    useShortcutStore.getState().rebind('room-description', 'Alt+R')
    const saved = JSON.parse(localStorage.getItem('tapestry:shortcuts') ?? '{}') as Record<string, string>
    expect(saved['room-description']).toBe('Alt+R')
  })

  it('rebind is no-op for unknown id', () => {
    expect(() => {
      useShortcutStore.getState().rebind('nonexistent', 'Alt+R')
    }).not.toThrow()
  })

  it('unregister removes the entry', () => {
    useShortcutStore.getState().register('room-description', 'Room description', 'Alt+L', vi.fn())
    useShortcutStore.getState().unregister('room-description')
    expect(useShortcutStore.getState().shortcuts.get('room-description')).toBeUndefined()
  })
})
