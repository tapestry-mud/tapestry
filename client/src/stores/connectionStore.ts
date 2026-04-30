import { create } from 'zustand'
import type { LoginPhaseState } from '../types/gmcp'

type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'error'

interface ConnectionState {
  status: ConnectionStatus
  serverAddress: string
  error: string | null
  loginPhase: LoginPhaseState
  setStatus: (status: ConnectionStatus) => void
  setServerAddress: (address: string) => void
  setError: (error: string | null) => void
  setLoginPhase: (loginPhase: LoginPhaseState) => void
}

export const useConnectionStore = create<ConnectionState>()((set) => ({
  status: 'disconnected',
  serverAddress: '',
  error: null,
  loginPhase: 'disconnected',
  setStatus: (status) => set({ status }),
  setServerAddress: (serverAddress) => set({ serverAddress }),
  setError: (error) => set({ error }),
  setLoginPhase: (loginPhase) => set({ loginPhase }),
}))
