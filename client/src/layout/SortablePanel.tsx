import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { DragHandleContext } from './DragHandleContext'

interface SortablePanelProps {
  id: string
  children: React.ReactNode
}

export function SortablePanel({ id, children }: SortablePanelProps) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id })

  const style: React.CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
  }

  return (
    <DragHandleContext.Provider value={{ listeners }}>
      <div ref={setNodeRef} style={style} {...attributes}>
        {children}
      </div>
    </DragHandleContext.Provider>
  )
}
