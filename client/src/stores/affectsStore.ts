import { create } from 'zustand'
import type { Affect } from '../types/game'

interface AffectsState {
  affects: Affect[]
  setAffects: (affects: Affect[]) => void
  tickTimers: () => void
}

export const useAffectsStore = create<AffectsState>()((set) => ({
  affects: [],
  setAffects: (affects) => set({ affects }),
  tickTimers: () =>
    set((s) => ({
      affects: s.affects
        .map((a) => ({ ...a, duration: a.duration > 0 ? a.duration - 1 : a.duration }))
        .filter((a) => a.duration !== 0),
    })),
}))
