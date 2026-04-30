import { create } from 'zustand'

export type DebugTab = 'gmcp' | 'text' | 'state' | 'connection' | 'commands'

interface GmcpLogEntry     { timestamp: number; package: string; data: unknown; direction: 'in' | 'out' }
interface TextLogEntry     { timestamp: number; raw: string }
interface CommandLogEntry  { timestamp: number; command: string }
interface ConnectionLogEntry { timestamp: number; event: string; detail: string }

const cap = <T>(arr: T[], max: number): T[] =>
  arr.length > max ? arr.slice(arr.length - max) : arr

interface DebugState {
  gmcpLog: GmcpLogEntry[]
  textLog: TextLogEntry[]
  commandLog: CommandLogEntry[]
  connectionLog: ConnectionLogEntry[]
  isOpen: boolean
  activeTab: DebugTab
  logGmcp: (pkg: string, data: unknown, direction: 'in' | 'out') => void
  logText: (raw: string) => void
  logCommand: (command: string) => void
  logConnection: (event: string, detail: string) => void
  toggleOpen: () => void
  setTab: (tab: DebugTab) => void
}

export const useDebugStore = create<DebugState>()((set) => ({
  gmcpLog: [], textLog: [], commandLog: [], connectionLog: [],
  isOpen: false, activeTab: 'gmcp',
  logGmcp: (pkg, data, direction) =>
    set((s) => ({ gmcpLog: cap([...s.gmcpLog, { timestamp: Date.now(), package: pkg, data, direction }], 500) })),
  logText: (raw) =>
    set((s) => ({ textLog: cap([...s.textLog, { timestamp: Date.now(), raw }], 500) })),
  logCommand: (command) =>
    set((s) => ({ commandLog: cap([...s.commandLog, { timestamp: Date.now(), command }], 200) })),
  logConnection: (event, detail) =>
    set((s) => ({ connectionLog: cap([...s.connectionLog, { timestamp: Date.now(), event, detail }], 200) })),
  toggleOpen: () => set((s) => ({ isOpen: !s.isOpen })),
  setTab: (activeTab) => set({ activeTab }),
}))
