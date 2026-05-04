import { create } from 'zustand'

export interface HelpTopicData {
    id: string
    title: string
    category: string
    brief: string
    body: string
    syntax: string[]
    seeAlso: string[]
}

export interface HelpTopicSummary {
    id: string
    title: string
    brief: string
}

export type HelpResponse =
    | { status: 'ok'; topic: HelpTopicData }
    | { status: 'multiple'; term: string; matches: HelpTopicSummary[] }
    | { status: 'no_match'; term: string }

interface HelpState {
    isOpen: boolean
    response: HelpResponse | null
    dialogKey: number
    inDialogKey: number
    inDialogAnnouncement: string
    openHelp: (response: HelpResponse) => void
    closeHelp: () => void
    pushAnnouncement: (text: string) => void
}

let announcementTimer: ReturnType<typeof setTimeout> | null = null

export const useHelpStore = create<HelpState>()((set) => ({
    isOpen: false,
    response: null,
    dialogKey: 0,
    inDialogKey: 0,
    inDialogAnnouncement: '',
    openHelp: (response) => {
        if (announcementTimer) { clearTimeout(announcementTimer) }
        set((state) => ({
            isOpen: true,
            response,
            dialogKey: state.dialogKey + 1,
            inDialogAnnouncement: '',
        }))
    },
    closeHelp: () => {
        if (announcementTimer) { clearTimeout(announcementTimer) }
        set({ isOpen: false, inDialogAnnouncement: '' })
    },
    pushAnnouncement: (text) => {
        if (announcementTimer) { clearTimeout(announcementTimer) }
        set((state) => ({ inDialogKey: state.inDialogKey + 1, inDialogAnnouncement: text }))
        announcementTimer = setTimeout(() => {
            set({ inDialogAnnouncement: '' })
            announcementTimer = null
        }, 1000)
    },
}))
