import { create } from 'zustand'
import type { ChatMessage } from '../types/game'

const MAX_MESSAGES = 500

interface ChatState {
  messages: ChatMessage[]
  activeFilter: string
  unreadCount: number
  addMessage: (data: { channel: string; sender: string; text: string }) => void
  setFilter: (channel: string) => void
  markRead: () => void
}

export const useChatStore = create<ChatState>()((set) => ({
  messages: [],
  activeFilter: 'all',
  unreadCount: 0,
  addMessage: (data) =>
    set((s) => {
      const msg: ChatMessage = { id: `${Date.now()}-${Math.random()}`, ...data, timestamp: Date.now() }
      const messages = [...s.messages, msg]
      if (messages.length > MAX_MESSAGES) { messages.splice(0, messages.length - MAX_MESSAGES) }
      return { messages, unreadCount: s.unreadCount + 1 }
    }),
  setFilter: (activeFilter) => set({ activeFilter }),
  markRead: () => set({ unreadCount: 0 }),
}))
