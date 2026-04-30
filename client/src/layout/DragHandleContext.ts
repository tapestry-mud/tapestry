import { createContext, useContext } from 'react'
import type { DraggableSyntheticListeners } from '@dnd-kit/core'

interface DragHandleContextValue {
  listeners: DraggableSyntheticListeners | undefined
}

export const DragHandleContext = createContext<DragHandleContextValue>({
  listeners: undefined,
})

export function useDragHandle() {
  return useContext(DragHandleContext)
}
