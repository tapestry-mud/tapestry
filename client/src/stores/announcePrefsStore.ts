import { create } from 'zustand'

export type AnnouncePref = 'assertive' | 'polite' | 'off'

export type AnnounceCategory = 'vitals' | 'chat' | 'combat' | 'room' | 'feedback'

const STORAGE_KEY = 'tapestry:announce-prefs'

const DEFAULTS: Record<AnnounceCategory, AnnouncePref> = {
  vitals: 'assertive',
  chat: 'polite',
  combat: 'assertive',
  room: 'polite',
  feedback: 'polite',
}

function loadPrefs(): Record<AnnounceCategory, AnnouncePref> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (raw) {
      const parsed = JSON.parse(raw)
      return { ...DEFAULTS, ...parsed }
    }
  } catch { /* use defaults */ }
  return { ...DEFAULTS }
}

function savePrefs(prefs: Record<AnnounceCategory, AnnouncePref>) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs))
}

interface AnnouncePrefsState {
  prefs: Record<AnnounceCategory, AnnouncePref>
  setPref: (category: AnnounceCategory, value: AnnouncePref) => void
}

export const useAnnouncePrefsStore = create<AnnouncePrefsState>()((set, get) => ({
  prefs: loadPrefs(),
  setPref: (category, value) => {
    const next = { ...get().prefs, [category]: value }
    set({ prefs: next })
    savePrefs(next)
  },
}))
