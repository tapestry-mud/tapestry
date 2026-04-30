import { create } from 'zustand'
import type { Item } from '../types/game'

interface InventoryState {
  items: Item[]
  setItems: (items: Item[]) => void
}

export const useInventoryStore = create<InventoryState>()((set) => ({
  items: [],
  setItems: (items) => set({ items }),
}))
