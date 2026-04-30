import { create } from 'zustand'
import type { Entity } from '../types/game'

interface NearbyState {
  entities: Entity[]
  setEntities: (entities: Entity[]) => void
}

export const useNearbyStore = create<NearbyState>()((set) => ({
  entities: [],
  setEntities: (entities) => set({ entities }),
}))
