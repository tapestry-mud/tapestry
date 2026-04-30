import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { RoomPanel } from './RoomPanel'
import { useRoomStore } from '../stores/roomStore'

const mockSend = vi.hoisted(() => vi.fn())
vi.mock('../connection/WebSocketClient', () => ({
  WebSocketClient: { send: mockSend },
}))

beforeEach(() => {
  useRoomStore.setState({
    current: { num: 1001, name: 'Town Square', area: 'Midgaard', environment: 'city', exits: { north: 1002 } },
    mapGraph: new Map([[1001, { num: 1001, name: 'Town Square', x: 0, y: 0, z: 0, exits: { north: 1002 } }]]),
    lastDirection: null,
  })
})

describe('RoomPanel', () => {
  it('renders current room name', () => {
    render(<RoomPanel />)
    expect(screen.getByText('Town Square')).toBeDefined()
  })

  it('renders area name', () => {
    render(<RoomPanel />)
    expect(screen.getByText(/Midgaard/)).toBeDefined()
  })

  it('renders compass buttons', () => {
    render(<RoomPanel />)
    expect(screen.getByTitle('north')).toBeDefined()
  })
})
