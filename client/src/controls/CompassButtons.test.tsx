import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { CompassButtons } from './CompassButtons'

const mockSend = vi.hoisted(() => vi.fn())
vi.mock('../connection/WebSocketClient', () => ({
  WebSocketClient: { send: mockSend },
}))

beforeEach(() => { mockSend.mockClear() })

describe('CompassButtons', () => {
  it('renders all 6 direction buttons', () => {
    render(<CompassButtons exits={{}} />)
    expect(screen.getByTitle('north')).toBeDefined()
    expect(screen.getByTitle('south')).toBeDefined()
    expect(screen.getByTitle('east')).toBeDefined()
    expect(screen.getByTitle('west')).toBeDefined()
    expect(screen.getByTitle('up')).toBeDefined()
    expect(screen.getByTitle('down')).toBeDefined()
  })

  it('available exit is not disabled', () => {
    render(<CompassButtons exits={{ north: 1 }} />)
    expect(screen.getByTitle('north')).not.toBeDisabled()
  })

  it('unavailable exit is disabled', () => {
    render(<CompassButtons exits={{ north: 1 }} />)
    expect(screen.getByTitle('south')).toBeDisabled()
  })

  it('clicking available exit calls WebSocketClient.send with direction', () => {
    render(<CompassButtons exits={{ north: 1 }} />)
    fireEvent.click(screen.getByTitle('north'))
    expect(mockSend).toHaveBeenCalledWith('north')
  })

  it('clicking disabled exit does not call send', () => {
    render(<CompassButtons exits={{}} />)
    fireEvent.click(screen.getByTitle('north'))
    expect(mockSend).not.toHaveBeenCalled()
  })
})
