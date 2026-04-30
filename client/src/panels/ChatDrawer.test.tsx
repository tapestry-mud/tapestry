import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { ChatDrawer } from './ChatDrawer'
import { useChatStore } from '../stores/chatStore'

const { mockSend } = vi.hoisted(() => ({ mockSend: vi.fn() }))
vi.mock('../connection/WebSocketClient', () => ({
  WebSocketClient: { send: mockSend },
}))

beforeEach(() => {
  mockSend.mockClear()
  useChatStore.setState({ messages: [], activeFilter: 'all', unreadCount: 0 })
})

describe('ChatDrawer', () => {
  it('shows open-chat tab when closed', () => {
    render(<ChatDrawer />)
    expect(screen.getByTitle('Open Chat Drawer')).toBeDefined()
  })

  it('shows unread badge on tab when messages exist', () => {
    useChatStore.setState({ messages: [], activeFilter: 'all', unreadCount: 3 })
    render(<ChatDrawer />)
    expect(screen.getByText('3')).toBeDefined()
  })

  it('opens drawer on tab click', () => {
    render(<ChatDrawer />)
    fireEvent.click(screen.getByTitle('Open Chat Drawer'))
    expect(screen.getByPlaceholderText('gossip hello...')).toBeDefined()
  })

  it('renders messages when open', () => {
    useChatStore.setState({
      messages: [{ id: '1', channel: 'chat', sender: 'Raegar', text: 'Hello!', timestamp: 1 }],
      activeFilter: 'all',
      unreadCount: 0,
    })
    render(<ChatDrawer />)
    fireEvent.click(screen.getByTitle('Open Chat Drawer'))
    expect(screen.getByText('Hello!')).toBeDefined()
  })

  it('marks read when opened', () => {
    useChatStore.setState({ messages: [], activeFilter: 'all', unreadCount: 5 })
    render(<ChatDrawer />)
    fireEvent.click(screen.getByTitle('Open Chat Drawer'))
    expect(useChatStore.getState().unreadCount).toBe(0)
  })

  it('filters messages by active channel tab', () => {
    useChatStore.setState({
      messages: [
        { id: '1', channel: 'chat', sender: 'A', text: 'chat msg', timestamp: 1 },
        { id: '2', channel: 'tell', sender: 'B', text: 'tell msg', timestamp: 2 },
      ],
      activeFilter: 'all',
      unreadCount: 0,
    })
    render(<ChatDrawer />)
    fireEvent.click(screen.getByTitle('Open Chat Drawer'))
    fireEvent.click(screen.getByText('tell'))
    expect(screen.getByText('tell msg')).toBeDefined()
    expect(screen.queryByText('chat msg')).toBeNull()
  })
})
