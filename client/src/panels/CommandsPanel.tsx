import { useMemo, useState } from 'react'
import { useCommandsStore } from '../stores/commandsStore'
import { PanelWrapper } from './PanelWrapper'

export function CommandsPanel() {
  const commands = useCommandsStore((s) => s.commands)
  const [search, setSearch] = useState('')
  const [activeCategory, setActiveCategory] = useState('all')

  const categories = useMemo(() => {
    const cats = [...new Set(commands.map((c) => c.category))].sort()
    return ['all', ...cats]
  }, [commands])

  const visible = useMemo(() => {
    const q = search.toLowerCase()
    return commands.filter((c) => {
      if (activeCategory !== 'all' && c.category !== activeCategory) { return false }
      if (!q) { return true }
      return (
        c.keyword.toLowerCase().includes(q) ||
        c.description.toLowerCase().includes(q) ||
        c.aliases.some((a) => a.toLowerCase().includes(q))
      )
    })
  }, [commands, search, activeCategory])

  if (commands.length === 0) {
    return (
      <PanelWrapper title="Commands">
        <div className="font-mono text-xs text-text-secondary">No command data</div>
      </PanelWrapper>
    )
  }

  return (
    <PanelWrapper title="Commands">
      <div className="flex flex-col gap-1 min-h-0" style={{ height: '240px' }}>
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search..."
          className="bg-surface border border-border rounded px-2 py-0.5 text-xs text-text-primary outline-none focus:border-accent shrink-0"
        />
        <div className="flex gap-1 overflow-x-auto shrink-0">
          {categories.map((cat) => (
            <button
              key={cat}
              onClick={() => setActiveCategory(cat)}
              className={`text-xs px-2 py-0.5 rounded whitespace-nowrap capitalize ${
                activeCategory === cat
                  ? 'bg-accent text-white'
                  : 'text-text-secondary hover:text-text-primary'
              }`}
            >
              {cat}
            </button>
          ))}
        </div>
        <div className="flex-1 overflow-y-auto min-h-0 flex flex-col gap-0.5">
          {visible.map((cmd) => (
            <div key={cmd.keyword} className="font-mono text-xs flex gap-2">
              <span className="text-text-primary shrink-0 w-24">{cmd.keyword}</span>
              {cmd.description
                ? <span className="text-text-secondary">{cmd.description}</span>
                : <span className="text-text-secondary opacity-50 italic">--</span>
              }
            </div>
          ))}
          {visible.length === 0 && (
            <div className="text-xs text-text-secondary italic">No matches</div>
          )}
        </div>
      </div>
    </PanelWrapper>
  )
}
