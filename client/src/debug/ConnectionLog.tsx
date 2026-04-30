import { useDebugStore } from '../stores/debugStore'
import { useConnectionStore } from '../stores/connectionStore'

export function ConnectionLog() {
  const { connectionLog } = useDebugStore()
  const { status, serverAddress } = useConnectionStore()

  return (
    <div className="flex flex-col h-full">
      <div className="px-2 py-1 border-b border-border shrink-0 flex gap-4 text-xs font-mono">
        <span className="text-text-secondary">Status: <span className="text-accent">{status}</span></span>
        <span className="text-text-secondary">Address: <span className="text-text-primary">{serverAddress || '(none)'}</span></span>
      </div>
      <div className="flex-1 overflow-y-auto p-2 font-mono text-xs flex flex-col gap-0.5">
        {connectionLog.length === 0 ? (
          <span className="text-text-secondary italic">No connection events</span>
        ) : (
          [...connectionLog].reverse().map((entry, i) => (
            <div key={i} className="flex gap-2">
              <span className="text-text-secondary shrink-0">{new Date(entry.timestamp).toISOString().slice(11, 23)}</span>
              <span className="text-ansi-yellow shrink-0">{entry.event}</span>
              <span className="text-text-secondary">{entry.detail}</span>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
