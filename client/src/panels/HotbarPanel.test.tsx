import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { HotbarPanel } from './HotbarPanel'
import { useHotbarStore } from '../stores/hotbarStore'

vi.mock('../connection/WebSocketClient', () => ({
  WebSocketClient: { send: vi.fn() },
}))

import { WebSocketClient } from '../connection/WebSocketClient'

beforeEach(() => {
  const emptySlots = Array(12).fill(null)
  useHotbarStore.setState({
    slots: emptySlots,
    setSlot: useHotbarStore.getState().setSlot,
    clearSlot: useHotbarStore.getState().clearSlot,
  })
  vi.clearAllMocks()
})

describe('HotbarPanel', () => {
  it('renders 12 slot buttons', () => {
    render(<HotbarPanel />)
    const buttons = screen.getAllByRole('button')
    expect(buttons.length).toBeGreaterThanOrEqual(12)
  })

  it('sends command when clicking a configured slot', () => {
    useHotbarStore.setState({
      slots: [{ emoji: '🔥', label: 'fb', command: 'fireball' }, ...Array(11).fill(null)],
      setSlot: useHotbarStore.getState().setSlot,
      clearSlot: useHotbarStore.getState().clearSlot,
    })
    render(<HotbarPanel />)
    const slot = screen.getByTitle(/fireball/)
    fireEvent.click(slot)
    expect(WebSocketClient.send).toHaveBeenCalledWith('fireball')
  })

  it('does not send when clicking an empty slot', () => {
    render(<HotbarPanel />)
    const buttons = screen.getAllByRole('button')
    fireEvent.click(buttons[0])
    expect(WebSocketClient.send).not.toHaveBeenCalled()
  })
})
