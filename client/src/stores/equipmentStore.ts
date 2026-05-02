import { create } from 'zustand'

interface EquipmentSlot {
  id: string
  name: string
  rarity?: string
  essence?: string
  rarityTag?: string
  essenceTag?: string
}

interface EquipmentState {
  slots: Record<string, EquipmentSlot | null | undefined>
  setEquipment: (slots: Record<string, EquipmentSlot | null | undefined>) => void
}

export const useEquipmentStore = create<EquipmentState>()((set) => ({
  slots: {},
  setEquipment: (slots) => set({ slots }),
}))
