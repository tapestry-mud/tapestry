import { useState, useRef, useEffect } from 'react'
import { WebSocketClient } from '../connection/WebSocketClient'
import { useConnectionStore } from '../stores/connectionStore'
import { useCommandBarStore } from '../stores/commandBarStore'

const MAX_HISTORY = 100

const STATUS_COLOR: Record<string, string> = {
  connected: 'bg-ansi-green',
  connecting: 'bg-ansi-yellow',
  disconnected: 'bg-ansi-bright-black',
  error: 'bg-ansi-red',
}

export function CommandBar() {
  const [value, setValue] = useState('')
  const status = useConnectionStore((s) => s.status)
  const loginPhase = useConnectionStore((s) => s.loginPhase)
  const isPassword = loginPhase === 'password'
  const inputRef = useRef<HTMLInputElement>(null)
  const historyRef = useRef<string[]>([])
  const historyIndexRef = useRef(-1)
  const savedInputRef = useRef('')
  const { pending, focusToken, clearPending } = useCommandBarStore()

  useEffect(() => {
    inputRef.current?.focus()
  }, [focusToken])

  useEffect(() => {
    if (!pending) { return }
    setValue(pending)
    clearPending()
    inputRef.current?.focus()
  }, [pending, clearPending])

  useEffect(() => {
    if (!isPassword) { setValue('') }
  }, [isPassword])

  function send() {
    const cmd = value.trim()
    if (!cmd) { return }
    WebSocketClient.send(cmd)
    if (!isPassword) {
      historyRef.current = [cmd, ...historyRef.current].slice(0, MAX_HISTORY)
    }
    historyIndexRef.current = -1
    savedInputRef.current = ''
    setValue('')
    inputRef.current?.focus()
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter') {
      send()
      return
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      const history = historyRef.current
      if (history.length === 0) { return }
      if (historyIndexRef.current === -1) { savedInputRef.current = value }
      const next = Math.min(historyIndexRef.current + 1, history.length - 1)
      historyIndexRef.current = next
      setValue(history[next])
      return
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      if (historyIndexRef.current === -1) { return }
      const next = historyIndexRef.current - 1
      historyIndexRef.current = next
      if (next === -1) {
        setValue(savedInputRef.current)
      } else {
        setValue(historyRef.current[next])
      }
    }
  }

  return (
    <div className="flex items-center gap-2 px-2 py-1.5 bg-surface-raised border-t border-border shrink-0">
      <span
        data-status={status}
        title={status}
        className={`w-2.5 h-2.5 rounded-full shrink-0 ${STATUS_COLOR[status] ?? 'bg-border'}`}
      />
      <input
        key={isPassword ? 'password' : 'text'}
        id="command-input"
        ref={inputRef}
        type={isPassword ? 'password' : 'text'}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Enter command..."
        aria-label="Game command input"
        className="flex-1 bg-surface border border-border rounded px-2 py-1 text-sm font-mono text-text-primary outline-none focus:border-accent"
        autoComplete="off"
        spellCheck={false}
        autoFocus
      />
      <button
        onClick={send}
        onMouseDown={(e) => e.preventDefault()}
        className="bg-accent text-white text-sm px-3 py-1 rounded hover:opacity-80"
      >
        Send
      </button>
    </div>
  )
}
