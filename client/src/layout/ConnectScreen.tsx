import { useState, useEffect } from 'react'
import { WebSocketClient } from '../connection/WebSocketClient'
import { useConnectionStore } from '../stores/connectionStore'

const RECENT_KEY = 'tapestry-recent-servers'
const LAST_KEY = 'tapestry-last-server'
const MAX_RECENT = 3

function loadRecent(): string[] {
  try {
    return JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]')
  } catch {
    return []
  }
}

function saveRecent(address: string) {
  const recent = [address, ...loadRecent().filter((a) => a !== address)].slice(0, MAX_RECENT)
  localStorage.setItem(RECENT_KEY, JSON.stringify(recent))
  localStorage.setItem(LAST_KEY, address)
}

export function ConnectScreen() {
  const { status, error } = useConnectionStore()
  const [address, setAddress] = useState(() => localStorage.getItem(LAST_KEY) ?? '')
  const [recent, setRecent] = useState<string[]>(loadRecent)
  const [autoConnectAttempted, setAutoConnectAttempted] = useState(false)

  const isConnecting = status === 'connecting'

  useEffect(() => {
    if (autoConnectAttempted) {
      return
    }
    const autoUrl = WebSocketClient.deriveServerUrl()
    if (autoUrl) {
      setAutoConnectAttempted(true)
      WebSocketClient.connect(autoUrl)
    }
  }, [autoConnectAttempted])

  function connect() {
    const addr = address.trim()
    if (!addr) { return }
    saveRecent(addr)
    setRecent(loadRecent())
    WebSocketClient.connect(addr)
  }

  // If auto-connecting, show a minimal connecting screen
  const autoUrl = WebSocketClient.deriveServerUrl()
  if (autoUrl && (status === 'connecting' || status === 'connected')) {
    return (
      <div className="flex items-center justify-center h-screen bg-surface">
        <div className="bg-surface-raised border border-border rounded-lg p-8 w-80 flex flex-col gap-4">
          <h1 className="text-text-primary text-xl font-bold font-ui text-center">Connecting...</h1>
        </div>
      </div>
    )
  }

  return (
    <div className="flex items-center justify-center h-screen bg-surface">
      <div className="bg-surface-raised border border-border rounded-lg p-8 w-80 flex flex-col gap-4">
        <h1 className="text-text-primary text-xl font-bold font-ui text-center">Tapestry</h1>
        <div className="flex flex-col gap-2">
          <label className="text-text-secondary text-sm font-ui">Server Address</label>
          <input
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { connect() } }}
            placeholder="host:4001"
            disabled={isConnecting}
            className="bg-surface border border-border rounded px-3 py-2 text-text-primary font-mono text-sm outline-none focus:border-accent disabled:opacity-50"
          />
        </div>
        {recent.length > 0 && (
          <div className="flex flex-col gap-1">
            <span className="text-text-secondary text-xs font-ui">Recent</span>
            {recent.map((r) => (
              <button
                key={r}
                onClick={() => setAddress(r)}
                className="text-left text-xs text-accent hover:text-text-primary font-mono"
              >
                {r}
              </button>
            ))}
          </div>
        )}
        {error && (
          <p className="text-ansi-red text-xs text-center">{error}</p>
        )}
        <button
          onClick={connect}
          disabled={isConnecting || !address.trim()}
          className="bg-accent text-white font-ui text-sm py-2 rounded hover:opacity-80 disabled:opacity-50"
        >
          {isConnecting ? 'Connecting...' : 'Connect'}
        </button>
      </div>
    </div>
  )
}
