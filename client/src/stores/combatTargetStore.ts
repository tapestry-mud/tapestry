import { create } from 'zustand'
import type { CharCombatTarget } from '../types/gmcp'

interface CombatTargetState {
  active: boolean
  name: string
  healthTier: string
  healthText: string
  update: (data: CharCombatTarget) => void
}

export const useCombatTargetStore = create<CombatTargetState>()((set) => ({
  active: false,
  name: '',
  healthTier: '',
  healthText: '',
  update: (data) =>
    set({
      active: data.active,
      name: data.name ?? '',
      healthTier: data.healthTier ?? '',
      healthText: data.healthText ?? '',
    }),
}))
