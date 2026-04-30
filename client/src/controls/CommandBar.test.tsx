import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { CommandBar } from './CommandBar'
import { useConnectionStore } from '../stores/connectionStore'

const mockSend = vi.hoisted(() => vi.fn())
vi.mock('../connection/WebSocketClient', () => ({
  WebSocketClient: { send: mockSend },
}))

beforeEach(() => {
  mockSend.mockClear()
  useConnectionStore.setState({ status: 'connected', loginPhase: 'name', serverAddress: '', error: null })
})

describe('CommandBar', () => {
  it('renders text input', () => {
    render(<CommandBar />)
    expect(screen.getByRole('textbox')).toBeDefined()
  })

  it('sends command on Enter and clears input', () => {
    render(<CommandBar />)
    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: 'look' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    expect(mockSend).toHaveBeenCalledWith('look')
    expect((input as HTMLInputElement).value).toBe('')
  })

  it('does not send empty input', () => {
    render(<CommandBar />)
    fireEvent.keyDown(screen.getByRole('textbox'), { key: 'Enter' })
    expect(mockSend).not.toHaveBeenCalled()
  })

  it('cycles back through history on ArrowUp', () => {
    render(<CommandBar />)
    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: 'look' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    fireEvent.keyDown(input, { key: 'ArrowUp' })
    expect((input as HTMLInputElement).value).toBe('look')
  })

  it('cycles forward through history on ArrowDown', () => {
    render(<CommandBar />)
    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: 'north' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    fireEvent.change(input, { target: { value: 'look' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    fireEvent.keyDown(input, { key: 'ArrowUp' })
    fireEvent.keyDown(input, { key: 'ArrowUp' })
    fireEvent.keyDown(input, { key: 'ArrowDown' })
    expect((input as HTMLInputElement).value).toBe('look')
  })

  it('shows connected status indicator', () => {
    const { container } = render(<CommandBar />)
    expect(container.querySelector('[data-status="connected"]')).not.toBeNull()
  })

  it('shows disconnected status indicator when not connected', () => {
    useConnectionStore.setState({ status: 'disconnected', serverAddress: '', error: null })
    const { container } = render(<CommandBar />)
    expect(container.querySelector('[data-status="disconnected"]')).not.toBeNull()
  })

  it('input is type=text during name phase', () => {
    const { getByRole } = render(<CommandBar />)
    const input = getByRole('textbox')
    expect(input).toHaveAttribute('type', 'text')
  })

  it('input is type=password during password phase', () => {
    useConnectionStore.setState({ loginPhase: 'password' } as Parameters<typeof useConnectionStore.setState>[0])
    const { container } = render(<CommandBar />)
    const input = container.querySelector('input')!
    expect(input.type).toBe('password')
  })

  it('command is added to history during name phase', () => {
    const { getByRole } = render(<CommandBar />)
    const input = getByRole('textbox') as HTMLInputElement
    fireEvent.change(input, { target: { value: 'look' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    fireEvent.keyDown(input, { key: 'ArrowUp' })
    expect(input.value).toBe('look')
  })

  it('command is NOT added to history during password phase', () => {
    useConnectionStore.setState({ loginPhase: 'password' } as Parameters<typeof useConnectionStore.setState>[0])
    const { container } = render(<CommandBar />)
    const input = container.querySelector('input')!
    fireEvent.change(input, { target: { value: 'secretpass' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    fireEvent.keyDown(input, { key: 'ArrowUp' })
    expect(input.value).toBe('')
  })
})
