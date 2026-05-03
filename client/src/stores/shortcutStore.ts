import { create } from 'zustand'

const STORAGE_KEY = 'tapestry:shortcuts'

export interface ShortcutEntry {
  id: string
  label: string
  key: string
  handler: () => void
  enabled: boolean
}

interface ShortcutState {
  // Runtime-only: Map and handler functions are not serializable; do not add persist middleware.
  shortcuts: Map<string, ShortcutEntry>
  register: (id: string, label: string, defaultKey: string, handler: () => void) => void
  unregister: (id: string) => void
  rebind: (id: string, newKey: string) => void
  getByKey: (key: string) => ShortcutEntry | undefined
}

function readSavedBinding(id: string): string | undefined {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) {
      const saved = JSON.parse(raw) as Record<string, string>
      return saved[id]
    }
  } catch { /* ignore corrupt storage */ }
  return undefined
}

function persistBinding(id: string, key: string): void {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    const saved = raw ? (JSON.parse(raw) as Record<string, string>) : {}
    saved[id] = key
    localStorage.setItem(STORAGE_KEY, JSON.stringify(saved))
  } catch { /* ignore */ }
}

export const useShortcutStore = create<ShortcutState>()((set, get) => ({
  shortcuts: new Map(),

  register: (id, label, defaultKey, handler) => {
    set((s) => {
      const next = new Map(s.shortcuts)
      const existing = next.get(id)
      if (existing) {
        next.set(id, { ...existing, handler })
        return { shortcuts: next }
      }
      const savedKey = readSavedBinding(id)
      const key = savedKey ?? defaultKey
      next.set(id, { id, label, key, handler, enabled: true })
      return { shortcuts: next }
    })
  },

  unregister: (id) => {
    set((s) => {
      const next = new Map(s.shortcuts)
      next.delete(id)
      return { shortcuts: next }
    })
  },

  rebind: (id, newKey) => {
    set((s) => {
      const entry = s.shortcuts.get(id)
      if (!entry) { return s }
      const next = new Map(s.shortcuts)
      next.set(id, { ...entry, key: newKey })
      persistBinding(id, newKey)
      return { shortcuts: next }
    })
  },

  getByKey: (key) => {
    const { shortcuts } = get()
    for (const entry of shortcuts.values()) {
      if (entry.key === key && entry.enabled) { return entry }
    }
    return undefined
  },
}))
