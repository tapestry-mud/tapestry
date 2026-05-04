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
    openHelp: (response: HelpResponse) => void
    closeHelp: () => void
}

export const useHelpStore = create<HelpState>()((set) => ({
    isOpen: false,
    response: null,
    openHelp: (response) => { set({ isOpen: true, response }) },
    closeHelp: () => { set({ isOpen: false }) },
}))
