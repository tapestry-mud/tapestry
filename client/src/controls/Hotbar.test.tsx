import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { Hotbar } from './Hotbar'
import { useHotbarStore } from '../stores/hotbarStore'

const mockSend = vi.hoisted(() => vi.fn())
vi.mock('../connection/WebSocketClient', () => ({
  WebSocketClient: { send: mockSend },
}))

beforeEach(() => {
  mockSend.mockClear()
  useHotbarStore.setState({ slots: Array(10).fill(null) })
})

describe('Hotbar', () => {
  it('renders 10 slot buttons', () => {
    render(<Hotbar />)
    expect(screen.getAllByRole('button')).toHaveLength(10)
  })

  it('clicking configured slot fires its command', () => {
    useHotbarStore.setState({
      slots: [{ emoji: '⚔️', label: 'atk', command: 'attack' }, ...Array(9).fill(null)],
    })
    render(<Hotbar />)
    fireEvent.click(screen.getAllByRole('button')[0])
    expect(mockSend).toHaveBeenCalledWith('attack')
  })

  it('clicking empty slot does not call send', () => {
    render(<Hotbar />)
    fireEvent.click(screen.getAllByRole('button')[0])
    expect(mockSend).not.toHaveBeenCalled()
  })

  it('configured slot shows emoji and label', () => {
    useHotbarStore.setState({
      slots: [{ emoji: '⚔️', label: 'atk', command: 'attack' }, ...Array(9).fill(null)],
    })
    render(<Hotbar />)
    expect(screen.getByText('⚔️')).toBeDefined()
    expect(screen.getByText('atk')).toBeDefined()
  })

  it('empty slot shows + hint', () => {
    render(<Hotbar />)
    expect(screen.getAllByText('+')).toHaveLength(10)
  })
})
