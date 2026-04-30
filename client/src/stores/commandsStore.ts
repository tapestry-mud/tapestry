import { create } from 'zustand'
import type { CharCommands } from '../types/gmcp'

export interface CommandEntry {
  keyword: string
  category: string
  description: string
  aliases: string[]
}

interface CommandsState {
  commands: CommandEntry[]
  setCommands: (data: CharCommands) => void
}

export const useCommandsStore = create<CommandsState>()((set) => ({
  commands: [],
  setCommands: (data) => set({ commands: data.commands }),
}))
