import { useState } from 'react'
import { useDebugStore } from '../stores/debugStore'

export function GmcpLog() {
  const { gmcpLog } = useDebugStore()
  const [filter, setFilter] = useState('')

  const visible = filter
    ? gmcpLog.filter((e) => e.package.toLowerCase().includes(filter.toLowerCase()))
    : gmcpLog

  return (
    <div className="flex flex-col h-full">
      <div className="px-2 py-1 border-b border-border shrink-0">
        <input
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          placeholder="Filter by package..."
          className="bg-surface border border-border rounded px-2 py-0.5 text-xs text-text-primary w-48 outline-none focus:border-accent"
        />
      </div>
      <div className="flex-1 overflow-y-auto p-2 font-mono text-xs flex flex-col gap-0.5">
        {visible.length === 0 ? (
          <span className="text-text-secondary italic">No GMCP packets</span>
        ) : (
          [...visible].reverse().map((entry, i) => (
            <div key={i} className={`flex gap-2 ${entry.direction === 'in' ? 'text-ansi-bright-blue' : 'text-ansi-bright-green'}`}>
              <span className="text-text-secondary shrink-0">{new Date(entry.timestamp).toISOString().slice(11, 23)}</span>
              <span className="shrink-0">{entry.direction === 'in' ? 'S-C' : 'C-S'}</span>
              <span className="text-accent shrink-0">{entry.package}</span>
              <span className="text-text-secondary truncate">{JSON.stringify(entry.data)}</span>
            </div>
          ))
        )}
      </div>
    </div>
  )
}
