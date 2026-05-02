import { create } from 'zustand'
import { useAnnouncePrefsStore, type AnnounceCategory } from '../stores/announcePrefsStore'

type Priority = 'assertive' | 'polite'

interface AnnounceState {
  assertiveMessage: string
  politeMessage: string
  pushMessage: (message: string, priority: Priority) => void
}

let assertiveTimer: ReturnType<typeof setTimeout> | null = null
let politeTimer: ReturnType<typeof setTimeout> | null = null

export const useAnnounceStore = create<AnnounceState>()((set) => ({
  assertiveMessage: '',
  politeMessage: '',
  pushMessage: (message, priority) => {
    if (priority === 'assertive') {
      if (assertiveTimer) { clearTimeout(assertiveTimer) }
      set({ assertiveMessage: '' })
      requestAnimationFrame(() => {
        set({ assertiveMessage: message })
      })
      assertiveTimer = setTimeout(() => {
        set({ assertiveMessage: '' })
        assertiveTimer = null
      }, 8000)
    } else {
      if (politeTimer) { clearTimeout(politeTimer) }
      set({ politeMessage: '' })
      requestAnimationFrame(() => {
        set({ politeMessage: message })
      })
      politeTimer = setTimeout(() => {
        set({ politeMessage: '' })
        politeTimer = null
      }, 8000)
    }
  },
}))

export function announce(message: string, category: AnnounceCategory, fallbackPriority: Priority = 'polite') {
  const pref = useAnnouncePrefsStore.getState().prefs[category]
  if (pref === 'off') { return }
  const priority = pref === 'assertive' ? 'assertive' : pref === 'polite' ? 'polite' : fallbackPriority
  useAnnounceStore.getState().pushMessage(message, priority)
}
