import { create } from 'zustand'
import type { WorldTime } from '../types/gmcp'

interface WorldState {
  hour: number
  period: 'dawn' | 'day' | 'dusk' | 'night' | null
  dayCount: number
  weatherState: string | null
  setTime: (data: WorldTime) => void
  setWeather: (state: string) => void
}

export const useWorldStore = create<WorldState>()((set) => ({
  hour: 0,
  period: null,
  dayCount: 0,
  weatherState: null,
  setTime: (data) => set({ hour: data.hour, period: data.period, dayCount: data.dayCount }),
  setWeather: (state) => set({ weatherState: state }),
}))
