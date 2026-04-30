import { create } from 'zustand'
import type { CharExperience } from '../types/gmcp'

export interface XpTrack {
  name: string
  level: number
  xp: number
  xpToNext: number
  currentLevelThreshold: number
}

interface XpState {
  tracks: XpTrack[]
  setTracks: (data: CharExperience) => void
}

export const useXpStore = create<XpState>()((set) => ({
  tracks: [],
  setTracks: (data) => set({ tracks: data.tracks }),
}))
