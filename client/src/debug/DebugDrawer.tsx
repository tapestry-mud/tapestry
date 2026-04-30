import { useDebugStore } from '../stores/debugStore'
import { useCharStore } from '../stores/charStore'
import { GmcpLog } from './GmcpLog'
import { TextLog } from './TextLog'
import { StateInspector } from './StateInspector'
import { ConnectionLog } from './ConnectionLog'
import type { DebugTab } from '../stores/debugStore'

const TABS: { id: DebugTab; label: string }[] = [
  { id: 'gmcp', label: 'GMCP' },
  { id: 'text', label: 'Text' },
  { id: 'state', label: 'State' },
  { id: 'connection', label: 'Connection' },
  { id: 'commands', label: 'Commands' },
]

export function DebugDrawer() {
  const { isOpen, activeTab, setTab, commandLog } = useDebugStore()
  const isAdmin = useCharStore((s) => s.isAdmin)

  if (!isOpen || !isAdmin) { return null }

  return (
    <div className="fixed bottom-0 left-0 right-0 h-64 bg-surface-overlay border-t-2 border-accent flex flex-col z-40">
      <div className="flex items-center gap-0.5 px-2 py-1 bg-surface-raised border-b border-border shrink-0">
        <span className="text-accent text-xs font-bold mr-2">DEBUG</span>
        {TABS.map((tab) => (
          <button
            key={tab.id}
            onClick={() => setTab(tab.id)}
            className={`text-xs px-2 py-0.5 rounded ${activeTab === tab.id ? 'bg-accent text-white' : 'text-text-secondary hover:text-text-primary'}`}
          >
            {tab.label}
          </button>
        ))}
        <div className="flex-1" />
        <span className="text-text-secondary text-xs">` to close</span>
      </div>
      <div className="flex-1 overflow-hidden">
        {activeTab === 'gmcp' && <GmcpLog />}
        {activeTab === 'text' && <TextLog />}
        {activeTab === 'state' && <StateInspector />}
        {activeTab === 'connection' && <ConnectionLog />}
        {activeTab === 'commands' && (
          <div className="h-full overflow-y-auto p-2 font-mono text-xs">
            {commandLog.length === 0 ? (
              <span className="text-text-secondary italic">No commands sent</span>
            ) : (
              [...commandLog].reverse().map((entry, i) => (
                <div key={i} className="flex gap-2 text-ansi-green">
                  <span className="text-text-secondary shrink-0">{new Date(entry.timestamp).toISOString().slice(11, 23)}</span>
                  <span>{entry.command}</span>
                </div>
              ))
            )}
          </div>
        )}
      </div>
    </div>
  )
}
