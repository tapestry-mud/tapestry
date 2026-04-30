import { useEffect, useReducer } from 'react'
import { useConnectionStore } from '../stores/connectionStore'
import { useCharStore } from '../stores/charStore'
import { useRoomStore } from '../stores/roomStore'
import { useChatStore } from '../stores/chatStore'
import { useOutputStore } from '../stores/outputStore'
import { useHotbarStore } from '../stores/hotbarStore'

export function StateInspector() {
  const [, tick] = useReducer((x: number) => x + 1, 0)

  useEffect(() => {
    const id = setInterval(tick, 1000)
    return () => clearInterval(id)
  }, [])

  const snapshot = {
    connection: useConnectionStore.getState(),
    char: useCharStore.getState(),
    room: {
      current: useRoomStore.getState().current,
      mapRoomCount: useRoomStore.getState().mapGraph.size,
    },
    chat: {
      messageCount: useChatStore.getState().messages.length,
      unreadCount: useChatStore.getState().unreadCount,
      activeFilter: useChatStore.getState().activeFilter,
    },
    output: { lineCount: useOutputStore.getState().lines.length },
    hotbar: useHotbarStore.getState().slots,
  }

  return (
    <div className="h-full overflow-y-auto p-2">
      <pre className="text-xs text-text-primary font-mono whitespace-pre-wrap break-all">
        {JSON.stringify(snapshot, null, 2)}
      </pre>
    </div>
  )
}
