import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { HelpModal } from './HelpModal'
import { useHelpStore } from '../stores/helpStore'

const { mockSend } = vi.hoisted(() => ({ mockSend: vi.fn() }))
vi.mock('../connection/WebSocketClient', () => ({
    WebSocketClient: { send: mockSend },
}))

// jsdom doesn't implement showModal/close natively
beforeEach(() => {
    HTMLDialogElement.prototype.showModal = vi.fn()
    HTMLDialogElement.prototype.close = vi.fn()
    mockSend.mockClear()
    useHelpStore.setState({ isOpen: false, response: null })
})

describe('HelpModal', () => {
    it('renders nothing visible when closed', () => {
        render(<HelpModal />)
        // dialog element exists but showModal was not called and no content is shown
        expect(HTMLDialogElement.prototype.showModal).not.toHaveBeenCalled()
        expect(screen.queryByRole('heading')).toBeNull()
    })

    it('renders topic title when opened with ok response', () => {
        useHelpStore.setState({
            isOpen: true,
            response: {
                status: 'ok',
                topic: { id: 'combat', title: 'Combat', category: 'combat', brief: 'Fight stuff.', body: 'Body here.', syntax: [], seeAlso: [] }
            }
        })
        render(<HelpModal />)
        expect(screen.getByText('Combat')).toBeTruthy()
        expect(screen.getByText('Fight stuff.', { selector: 'p' })).toBeTruthy()
    })

    it('renders disambiguation list when opened with multiple response', () => {
        useHelpStore.setState({
            isOpen: true,
            response: {
                status: 'multiple',
                term: 'fight',
                matches: [
                    { id: 'combat-basics', title: 'Combat Basics', brief: 'Basic combat.' },
                    { id: 'combat-advanced', title: 'Advanced Combat', brief: 'Advanced.' }
                ]
            }
        })
        render(<HelpModal />)
        expect(screen.getByText('combat-basics')).toBeTruthy()
        expect(screen.getByText('combat-advanced')).toBeTruthy()
    })

    it('calls closeHelp when X button is clicked', () => {
        useHelpStore.setState({
            isOpen: true,
            response: { status: 'ok', topic: { id: 'x', title: 'X', category: 'g', brief: 'b', body: 'b', syntax: [], seeAlso: [] } }
        })
        render(<HelpModal />)
        fireEvent.click(screen.getByLabelText('Close help'))
        expect(useHelpStore.getState().isOpen).toBe(false)
    })
})
