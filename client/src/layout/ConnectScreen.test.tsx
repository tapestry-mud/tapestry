import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { ConnectScreen } from './ConnectScreen'
import { useConnectionStore } from '../stores/connectionStore'

const mockConnect = vi.hoisted(() => vi.fn())
const mockDeriveServerUrl = vi.hoisted(() => vi.fn().mockReturnValue(null))
vi.mock('../connection/WebSocketClient', () => ({
  WebSocketClient: { connect: mockConnect, deriveServerUrl: mockDeriveServerUrl },
}))

beforeEach(() => {
  mockConnect.mockClear()
  useConnectionStore.setState({ status: 'disconnected', serverAddress: '', error: null })
  localStorage.clear()
})

describe('ConnectScreen', () => {
  it('renders server address input', () => {
    render(<ConnectScreen />)
    expect(screen.getByRole('textbox')).toBeDefined()
  })

  it('renders connect button', () => {
    render(<ConnectScreen />)
    expect(screen.getByRole('button', { name: /connect/i })).toBeDefined()
  })

  it('calls WebSocketClient.connect with entered address on button click', () => {
    render(<ConnectScreen />)
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'localhost:4001' } })
    fireEvent.click(screen.getByRole('button', { name: /connect/i }))
    expect(mockConnect).toHaveBeenCalledWith('localhost:4001')
  })

  it('calls connect on Enter key in input', () => {
    render(<ConnectScreen />)
    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: 'localhost:4001' } })
    fireEvent.keyDown(input, { key: 'Enter' })
    expect(mockConnect).toHaveBeenCalledWith('localhost:4001')
  })

  it('shows error message when connectionStore has error', () => {
    useConnectionStore.setState({ status: 'error', serverAddress: '', error: 'connection refused' })
    render(<ConnectScreen />)
    expect(screen.getByText(/connection refused/i)).toBeDefined()
  })

  it('pre-fills last server from localStorage', () => {
    localStorage.setItem('tapestry-last-server', 'myserver:4001')
    render(<ConnectScreen />)
    expect((screen.getByRole('textbox') as HTMLInputElement).value).toBe('myserver:4001')
  })
})
