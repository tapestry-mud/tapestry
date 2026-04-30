import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { HotbarSlot } from '../types/game'

interface HotbarState {
  slots: (HotbarSlot | null)[]
  setSlot: (index: number, config: HotbarSlot) => void
  clearSlot: (index: number) => void
}

export const useHotbarStore = create<HotbarState>()(
  persist(
    (set) => ({
      slots: Array(10).fill(null),
      setSlot: (index, config) =>
        set((s) => {
          if (index < 0 || index >= s.slots.length) { return s }
          const slots = [...s.slots]
          slots[index] = config
          return { slots }
        }),
      clearSlot: (index) =>
        set((s) => {
          if (index < 0 || index >= s.slots.length) { return s }
          const slots = [...s.slots]
          slots[index] = null
          return { slots }
        }),
    }),
    { name: 'tapestry-hotbar' }
  )
)
