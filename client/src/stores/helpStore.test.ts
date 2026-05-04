import { describe, it, expect, beforeEach } from 'vitest'
import { useHelpStore } from './helpStore'
import type { HelpResponse } from './helpStore'

const okResp: HelpResponse = {
    status: 'ok',
    topic: { id: 'test', title: 'Test', category: 'general', brief: 'brief', body: 'body', syntax: [], seeAlso: [] }
}

describe('helpStore', () => {
    beforeEach(() => {
        useHelpStore.setState({ isOpen: false, response: null })
    })

    it('starts closed with no response', () => {
        const state = useHelpStore.getState()
        expect(state.isOpen).toBe(false)
        expect(state.response).toBeNull()
    })

    it('openHelp sets isOpen true and stores response', () => {
        useHelpStore.getState().openHelp(okResp)
        expect(useHelpStore.getState().isOpen).toBe(true)
        expect(useHelpStore.getState().response).toEqual(okResp)
    })

    it('closeHelp sets isOpen false', () => {
        useHelpStore.getState().openHelp(okResp)
        useHelpStore.getState().closeHelp()
        expect(useHelpStore.getState().isOpen).toBe(false)
    })
})
