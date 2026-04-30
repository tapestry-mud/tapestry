import { useState } from 'react'
import { cn } from '@/lib/utils'
import { useDragHandle } from '../layout/DragHandleContext'

interface PanelWrapperProps {
  title: string
  count?: number
  defaultCollapsed?: boolean
  children: React.ReactNode
}

export function PanelWrapper({ title, count, defaultCollapsed = false, children }: PanelWrapperProps) {
  const [collapsed, setCollapsed] = useState(defaultCollapsed)
  const { listeners } = useDragHandle()

  return (
    <div className="border border-border rounded bg-surface-raised">
      <div className="flex items-center gap-2 px-2 py-1 select-none">
        <span
          className="text-text-secondary text-xs cursor-grab active:cursor-grabbing touch-none"
          {...(listeners ?? {})}
        >
          ⠿
        </span>
        <span className="text-text-primary text-sm font-ui flex-1">{title}</span>
        {count !== undefined && count > 0 && (
          <span className="text-xs bg-accent text-white rounded-full px-1.5 py-0.5 leading-none">
            {count}
          </span>
        )}
        <button
          onClick={() => setCollapsed((c) => !c)}
          className="text-text-secondary hover:text-text-primary transition-colors"
          aria-label={collapsed ? 'expand' : 'collapse'}
        >
          {collapsed ? '▶' : '▼'}
        </button>
      </div>
      {!collapsed && (
        <div className={cn('px-2 pb-2 animate-in slide-in-from-top-1 duration-150')}>
          {children}
        </div>
      )}
    </div>
  )
}
