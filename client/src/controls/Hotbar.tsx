import { useState } from 'react'
import { useHotbarStore } from '../stores/hotbarStore'
import { useCommandBarStore } from '../stores/commandBarStore'
import { WebSocketClient } from '../connection/WebSocketClient'
import type { HotbarSlot } from '../types/game'

interface SlotConfigPopoverProps {
  index: number
  current: HotbarSlot | null
  onClose: () => void
}

function SlotConfigPopover({ index, current, onClose }: SlotConfigPopoverProps) {
  const { setSlot, clearSlot } = useHotbarStore.getState()
  const [emoji, setEmoji] = useState(current?.emoji ?? '')
  const [label, setLabel] = useState(current?.label ?? '')
  const [command, setCommand] = useState(current?.command ?? '')

  function save() {
    if (emoji && command) {
      setSlot(index, { emoji, label: label.slice(0, 5), command })
    }
    onClose()
  }

  return (
    <div className="absolute bottom-full mb-1 left-0 bg-surface-overlay border border-border rounded p-2 z-30 flex flex-col gap-1 min-w-[140px]">
      <input value={emoji} onChange={(e) => setEmoji(e.target.value)} placeholder="Emoji" className="bg-surface border border-border rounded px-1 py-0.5 text-xs text-text-primary w-full" />
      <input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="Label (5 char)" maxLength={5} className="bg-surface border border-border rounded px-1 py-0.5 text-xs text-text-primary w-full" />
      <input value={command} onChange={(e) => setCommand(e.target.value)} placeholder="Command" className="bg-surface border border-border rounded px-1 py-0.5 text-xs text-text-primary w-full" />
      <div className="flex gap-1">
        <button onClick={save} className="flex-1 bg-accent text-white text-xs py-0.5 rounded">Save</button>
        {current && <button onClick={() => { clearSlot(index); onClose() }} className="text-xs text-ansi-red px-1">Clear</button>}
      </div>
    </div>
  )
}

export function Hotbar() {
  const { slots } = useHotbarStore()
  const [configOpen, setConfigOpen] = useState<number | null>(null)

  return (
    <div className="flex gap-1 px-2 py-1 bg-surface-raised border-t border-border shrink-0 relative">
      {slots.map((slot, i) => (
        <div key={i} className="relative">
          <button
            onClick={() => { if (slot) { WebSocketClient.send(slot.command) } }}
            onMouseDown={(e) => { if (e.button === 0) { e.preventDefault() } }}
            onContextMenu={(e) => { e.preventDefault(); setConfigOpen(i) }}
            title={slot ? `${slot.label} - ${slot.command}` : 'Right-click to configure'}
            className={`w-12 h-12 flex flex-col items-center justify-center rounded border text-xs gap-0.5
              ${slot ? 'border-accent hover:bg-accent hover:text-white cursor-pointer' : 'border-border opacity-50 cursor-default'}`}
          >
            {slot ? (
              <>
                <span className="text-base leading-none">{slot.emoji}</span>
                <span className="text-text-secondary leading-none">{slot.label}</span>
              </>
            ) : (
              <span className="text-text-secondary">+</span>
            )}
          </button>
          {configOpen === i && (
            <SlotConfigPopover index={i} current={slot} onClose={() => { setConfigOpen(null); useCommandBarStore.getState().requestFocus() }} />
          )}
        </div>
      ))}
    </div>
  )
}
