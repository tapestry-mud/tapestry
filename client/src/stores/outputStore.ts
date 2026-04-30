import { create } from 'zustand'
import type { AnsiToken } from '../types/game'

const MAX_LINES = 5000

interface OutputState {
  lines: AnsiToken[][]
  scrollLocked: boolean
  appendLine: (tokens: AnsiToken[]) => void
  appendSystemMessage: (text: string) => void
  clear: () => void
  setScrollLocked: (locked: boolean) => void
}

export const useOutputStore = create<OutputState>()((set) => ({
  lines: [],
  scrollLocked: false,
  appendLine: (tokens) =>
    set((s) => ({
      lines: s.lines.length >= MAX_LINES
        ? [...s.lines.slice(s.lines.length - MAX_LINES + 1), tokens]
        : [...s.lines, tokens],
    })),
  appendSystemMessage: (text) =>
    set((s) => {
      const tokens: AnsiToken[] = [{ text, styles: { fg: 'text-ansi-bright-black' } }]
      return {
        lines: s.lines.length >= MAX_LINES
          ? [...s.lines.slice(s.lines.length - MAX_LINES + 1), tokens]
          : [...s.lines, tokens],
      }
    }),
  clear: () => set({ lines: [] }),
  setScrollLocked: (scrollLocked) => set({ scrollLocked }),
}))
