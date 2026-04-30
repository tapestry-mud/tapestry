import { create } from 'zustand'
import { useCharStore } from './charStore'

export interface PanelLayout {
  id: string
  column: 'left' | 'right'
  order: number
}

const DEFAULT_PANELS: PanelLayout[] = [
  { id: 'CharacterPanel',   column: 'left',  order: 0 },
  { id: 'StatsPanel',       column: 'left',  order: 1 },
  { id: 'VitalsPanel',      column: 'left',  order: 2 },
  { id: 'EffectsPanel',     column: 'left',  order: 3 },
  { id: 'CombatTargetPanel',  column: 'left',  order: 4 },
  { id: 'XPPanel',          column: 'left',  order: 5 },
  { id: 'MapPanel',         column: 'right', order: 0 },
  { id: 'TimeWeatherPanel', column: 'right', order: 1 },
  { id: 'EquipmentPanel',   column: 'right', order: 2 },
  { id: 'InventoryPanel',   column: 'right', order: 3 },
  { id: 'ChatPanel',        column: 'right', order: 4 },
  { id: 'NearbyPanel',      column: 'right', order: 5 },
]

function layoutStorageKey(charName: string): string {
  return `tapestry:layout:${charName}`
}

function saveLayout(charName: string, panels: PanelLayout[]): void {
  if (!charName) { return }
  localStorage.setItem(layoutStorageKey(charName), JSON.stringify(panels))
}

interface LayoutState {
  panels: PanelLayout[]
  loadLayoutForCharacter: (charName: string) => void
  setPanelColumn: (id: string, column: 'left' | 'right') => void
  setPanelOrder: (id: string, order: number) => void
  resetLayout: () => void
}

export const useLayoutStore = create<LayoutState>()((set, get) => ({
  panels: DEFAULT_PANELS,

  loadLayoutForCharacter: (charName) => {
    if (!charName) { return }
    try {
      const raw = localStorage.getItem(layoutStorageKey(charName))
      if (raw) {
        const saved = JSON.parse(raw) as PanelLayout[]
        const savedIds = new Set(saved.map((p) => p.id))
        const missing = DEFAULT_PANELS.filter((p) => !savedIds.has(p.id))
        const panels = missing.length > 0 ? [...saved, ...missing] : saved
        if (missing.length > 0) { saveLayout(charName, panels) }
        set({ panels })
        return
      }
    } catch {
      // corrupt -- fall through to defaults
    }
    set({ panels: DEFAULT_PANELS })
    saveLayout(charName, DEFAULT_PANELS)
  },

  setPanelColumn: (id, column) =>
    set((s) => {
      const panels = s.panels.map((p) => p.id === id ? { ...p, column } : p)
      saveLayout(useCharStore.getState().name, panels)
      return { panels }
    }),

  setPanelOrder: (id, order) =>
    set((s) => {
      const panels = s.panels.map((p) => p.id === id ? { ...p, order } : p)
      saveLayout(useCharStore.getState().name, panels)
      return { panels }
    }),

  resetLayout: () => {
    const charName = useCharStore.getState().name
    set({ panels: DEFAULT_PANELS })
    saveLayout(charName, DEFAULT_PANELS)
  },
}))
