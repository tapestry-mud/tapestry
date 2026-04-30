import { useEffect, useRef, useState } from 'react'
import { useChatStore } from '../stores/chatStore'
import { useCommandBarStore } from '../stores/commandBarStore'
import { WebSocketClient } from '../connection/WebSocketClient'
import { PanelWrapper } from './PanelWrapper'

const CHANNELS = ['all', 'say', 'gossip', 'yell', 'clan', 'group', 'imm', 'tell']

export function ChatPanel() {
  const [input, setInput] = useState('')
  const { messages, activeFilter, setFilter, markRead } = useChatStore()
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => { markRead() }, [markRead])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
    markRead()
  }, [messages, markRead])

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

  return (
    <PanelWrapper title="Chat">
      <div className="flex flex-col gap-1 min-h-0" style={{ height: '200px' }}>
        <div className="flex gap-1 overflow-x-auto shrink-0">
          {CHANNELS.map((ch) => (
            <button
              key={ch}
              onClick={() => setFilter(ch)}
              className={`text-xs px-2 py-0.5 rounded whitespace-nowrap ${
                activeFilter === ch
                  ? 'bg-accent text-white'
                  : 'text-text-secondary hover:text-text-primary'
              }`}
            >
              {ch}
            </button>
          ))}
        </div>
        <div className="flex-1 overflow-y-auto flex flex-col gap-0.5 min-h-0">
          {visible.map((msg) => (
            <div key={msg.id} className="text-xs">
              <span className="text-text-secondary">[{msg.channel}]</span>{' '}
              <span className="text-ansi-yellow font-bold">{msg.sender}:</span>{' '}
              <span className="text-text-primary">{msg.text}</span>
            </div>
          ))}
          <div ref={bottomRef} />
        </div>
        <div className="flex gap-1 shrink-0">
          <input
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { sendChat() } }}
            onBlur={() => { requestAnimationFrame(() => useCommandBarStore.getState().requestFocus()) }}
            placeholder={
              activeFilter === 'all' ? 'type a command...' :
              activeFilter === 'tell' ? 'tell player message...' :
              `${activeFilter} message...`
            }
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
    </PanelWrapper>
  )
}
