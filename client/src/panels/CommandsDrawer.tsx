import { useMemo, useState } from 'react'
import { useCommandsStore } from '../stores/commandsStore'
import { useCommandBarStore } from '../stores/commandBarStore'

export function CommandsDrawer() {
  const commands = useCommandsStore((s) => s.commands)
  const setPending = useCommandBarStore((s) => s.setPending)
  const [isOpen, setIsOpen] = useState(false)
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

  if (!isOpen) {
    return (
      <button
        title="Open Commands"
        onClick={() => setIsOpen(true)}
        className="fixed bottom-11 left-4 bg-surface-overlay border border-b-0 border-border rounded-t px-3 py-1 z-20 text-xs font-ui text-text-primary hover:text-accent transition-colors"
      >
        Commands
      </button>
    )
  }

  return (
    <div className="fixed left-0 top-0 h-full w-[60%] bg-surface-raised border-r border-border flex flex-col z-20">
      <div className="flex items-center justify-between px-3 py-2 border-b border-border shrink-0">
        <span className="text-text-primary font-ui text-sm font-bold">Commands</span>
        <button onClick={() => setIsOpen(false)} className="text-text-secondary hover:text-text-primary text-lg leading-none">x</button>
      </div>
      <div className="px-2 py-1 border-b border-border shrink-0">
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search..."
          className="w-full bg-surface border border-border rounded px-2 py-0.5 text-xs text-text-primary outline-none focus:border-accent"
        />
      </div>
      <div className="flex gap-1 px-2 py-1 border-b border-border overflow-x-auto shrink-0">
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
      <div className="flex-1 overflow-y-auto px-2 py-1 flex flex-col gap-0.5">
        {visible.map((cmd) => (
          <div
            key={cmd.keyword}
            onClick={() => { setPending(cmd.keyword + ' '); setIsOpen(false) }}
            className="font-mono text-xs flex gap-2 cursor-pointer hover:bg-surface-overlay rounded px-1 -mx-1 py-0.5"
          >
            <span className="text-text-primary shrink-0 w-24">{cmd.keyword}</span>
            {cmd.description
              ? <span className="text-text-secondary">{cmd.description}</span>
              : <span className="text-text-secondary opacity-40 italic">--</span>
            }
          </div>
        ))}
        {visible.length === 0 && (
          <div className="text-xs text-text-secondary italic">No matches</div>
        )}
      </div>
    </div>
  )
}
