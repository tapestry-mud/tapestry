import { useState } from 'react'
import { useChatStore } from '../stores/chatStore'
import { useCommandBarStore } from '../stores/commandBarStore'
import { WebSocketClient } from '../connection/WebSocketClient'

const CHANNELS = ['all', 'gossip', 'yell', 'clan', 'group', 'imm', 'tell']

export function ChatDrawer() {
  const [isOpen, setIsOpen] = useState(false)
  const [input, setInput] = useState('')
  const { messages, activeFilter, unreadCount, setFilter, markRead } = useChatStore()

  function openDrawer() {
    setIsOpen(true)
    markRead()
  }

  function closeDrawer() {
    setIsOpen(false)
    useCommandBarStore.getState().requestFocus()
  }

  function sendChat() {
    const text = input.trim()
    if (!text) { return }
    const cmd = activeFilter !== 'all' ? `${activeFilter} ${text}` : text
    WebSocketClient.send(cmd)
    setInput('')
    useCommandBarStore.getState().requestFocus()
  }

  const visible = activeFilter === 'all'
    ? messages
    : messages.filter((m) => m.channel === activeFilter)

  if (!isOpen) {
    return (
      <button
        title="Open Chat Drawer"
        onClick={openDrawer}
        className="fixed right-0 top-1/2 -translate-y-1/2 bg-surface-raised border border-border rounded-l px-1 py-3 flex flex-col items-center gap-1 z-20"
      >
        <span className="text-text-secondary text-xs [writing-mode:vertical-rl]">Chat</span>
        {unreadCount > 0 && (
          <span className="bg-accent text-white text-xs rounded-full w-5 h-5 flex items-center justify-center">
            {unreadCount}
          </span>
        )}
      </button>
    )
  }

  return (
    <div className="fixed right-0 top-0 h-full w-[35%] bg-surface-raised border-l border-border flex flex-col z-20">
      <div className="flex items-center justify-between px-3 py-2 border-b border-border">
        <span className="text-text-primary font-ui text-sm font-bold">Chat</span>
        <button onClick={closeDrawer} className="text-text-secondary hover:text-text-primary text-lg leading-none">x</button>
      </div>
      <div className="flex gap-1 px-2 py-1 border-b border-border overflow-x-auto">
        {CHANNELS.map((ch) => (
          <button
            key={ch}
            onClick={() => setFilter(ch)}
            className={`text-xs px-2 py-0.5 rounded whitespace-nowrap ${activeFilter === ch ? 'bg-accent text-white' : 'text-text-secondary hover:text-text-primary'}`}
          >
            {ch}
          </button>
        ))}
      </div>
      <div className="flex-1 overflow-y-auto px-2 py-1 flex flex-col gap-0.5">
        {visible.map((msg) => (
          <div key={msg.id} className="text-xs">
            <span className="text-text-secondary">[{msg.channel}]</span>{' '}
            <span className="text-ansi-yellow font-bold">{msg.sender}:</span>{' '}
            <span className="text-text-primary">{msg.text}</span>
          </div>
        ))}
      </div>
      <div className="flex gap-1 p-2 border-t border-border">
        <input
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') { sendChat() } }}
          onBlur={() => { requestAnimationFrame(() => useCommandBarStore.getState().requestFocus()) }}
          placeholder={activeFilter === 'all' ? 'gossip hello...' : activeFilter === 'tell' ? 'player message...' : 'message...'}
          className="flex-1 bg-surface border border-border rounded px-2 py-1 text-xs text-text-primary outline-none focus:border-accent"
        />
        <button
          onClick={sendChat}
          onMouseDown={(e) => e.preventDefault()}
          className="bg-accent text-white text-xs px-2 py-1 rounded hover:opacity-80"
        >
          Send
        </button>
      </div>
    </div>
  )
}
