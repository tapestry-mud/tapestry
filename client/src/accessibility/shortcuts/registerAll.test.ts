import { beforeEach, describe, it, expect } from 'vitest'
import { useShortcutStore } from '../../stores/shortcutStore'
import { registerAllShortcuts } from './registerAll'

beforeEach(() => {
  useShortcutStore.setState({ shortcuts: new Map() })
  localStorage.clear()
})

describe('registerAllShortcuts', () => {
  it('registers room-description on Alt+L', () => {
    registerAllShortcuts()
    const entry = useShortcutStore.getState().getByKey('Alt+L')
    expect(entry?.id).toBe('room-description')
    expect(entry?.enabled).toBe(true)
  })

  it('registers context-commands on Alt+C', () => {
    registerAllShortcuts()
    const entry = useShortcutStore.getState().getByKey('Alt+C')
    expect(entry?.id).toBe('context-commands')
    expect(entry?.enabled).toBe(true)
  })
})
