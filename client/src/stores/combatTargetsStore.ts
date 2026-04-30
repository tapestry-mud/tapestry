import { create } from 'zustand'
import type { CharCombatTargets } from '../types/gmcp'

type CombatTarget = CharCombatTargets['targets'][number]

interface CombatTargetsState {
  targets: CombatTarget[]
  update: (data: CharCombatTargets) => void
}

export const useCombatTargetsStore = create<CombatTargetsState>()((set) => ({
  targets: [],
  update: (data) => set({ targets: data.targets }),
}))
