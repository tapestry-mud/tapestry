import { create } from 'zustand'

interface DisplayState {
  colorMap: Record<string, string>
  setColorMap: (map: Record<string, string>) => void
}

export const useDisplayStore = create<DisplayState>()((set) => ({
  colorMap: {},
  setColorMap: (colorMap) => { set({ colorMap }) },
}))
