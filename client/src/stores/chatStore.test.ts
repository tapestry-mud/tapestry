import { beforeEach, describe, it, expect } from 'vitest'
import { useChatStore } from './chatStore'

beforeEach(() => { useChatStore.setState({ messages: [], activeFilter: 'all', unreadCount: 0 }) })

describe('chatStore', () => {
  it('addMessage appends with id and timestamp', () => {
    useChatStore.getState().addMessage({ channel: 'chat', sender: 'Raegar', text: 'Hello' })
    const { messages } = useChatStore.getState()
    expect(messages).toHaveLength(1)
    expect(messages[0].sender).toBe('Raegar')
    expect(messages[0].id).toBeTruthy()
  })

  it('addMessage increments unreadCount', () => {
    useChatStore.getState().addMessage({ channel: 'chat', sender: 'x', text: 'y' })
    expect(useChatStore.getState().unreadCount).toBe(1)
  })

  it('markRead resets unreadCount to 0', () => {
    useChatStore.getState().addMessage({ channel: 'chat', sender: 'x', text: 'y' })
    useChatStore.getState().markRead()
    expect(useChatStore.getState().unreadCount).toBe(0)
  })

  it('setFilter changes activeFilter', () => {
    useChatStore.getState().setFilter('tell')
    expect(useChatStore.getState().activeFilter).toBe('tell')
  })

  it('caps at 500 messages, dropping oldest', () => {
    const store = useChatStore.getState()
    for (let i = 0; i < 505; i++) { store.addMessage({ channel: 'chat', sender: 'x', text: `${i}` }) }
    expect(useChatStore.getState().messages).toHaveLength(500)
    expect(useChatStore.getState().messages[0].text).toBe('5')
  })
})
