import { useState } from 'react'
import { useHotbarStore } from '../stores/hotbarStore'
import { WebSocketClient } from '../connection/WebSocketClient'
import { PanelWrapper } from './PanelWrapper'
import type { HotbarSlot } from '../types/game'

interface SlotConfigPopoverProps {
  index: number
  current: HotbarSlot | null
  onClose: () => void
}

function SlotConfigPopover({ index, current, onClose }: SlotConfigPopoverProps) {
  const setSlot = useHotbarStore((s) => s.setSlot)
  const clearSlot = useHotbarStore((s) => s.clearSlot)
  const [emoji, setEmoji] = useState(current?.emoji ?? '')
  const [label, setLabel] = useState(current?.label ?? '')
  const [command, setCommand] = useState(current?.command ?? '')

  function save() {
    if (emoji && command) {
      setSlot(index, { emoji, label: label.slice(0, 3), command })
    }
    onClose()
  }

  return (
    <div className="absolute bottom-full mb-1 left-0 bg-surface-overlay border border-border rounded p-2 z-30 flex flex-col gap-1 min-w-[140px]">
      <input
        value={emoji}
        onChange={(e) => setEmoji(e.target.value)}
        placeholder="Emoji"
        className="bg-surface border border-border rounded px-1 py-0.5 text-xs text-text-primary w-full"
      />
      <input
        value={label}
        onChange={(e) => setLabel(e.target.value)}
        placeholder="Label (3 char)"
        maxLength={3}
        className="bg-surface border border-border rounded px-1 py-0.5 text-xs text-text-primary w-full"
      />
      <input
        value={command}
        onChange={(e) => setCommand(e.target.value)}
        placeholder="Command"
        className="bg-surface border border-border rounded px-1 py-0.5 text-xs text-text-primary w-full"
      />
      <div className="flex gap-1">
        <button onClick={save} className="flex-1 bg-accent text-white text-xs py-0.5 rounded">
          Save
        </button>
        {current && (
          <button
            onClick={() => { clearSlot(index); onClose() }}
            className="text-xs text-ansi-red px-1"
          >
            Clear
          </button>
        )}
      </div>
    </div>
  )
}

export function HotbarPanel() {
  const { slots } = useHotbarStore()
  const [configOpen, setConfigOpen] = useState<number | null>(null)

  return (
    <PanelWrapper title="Hotbar">
      <div className="grid grid-cols-6 gap-0.5">
        {slots.map((slot, i) => (
          <div key={i} className="relative">
            <button
              onClick={() => { if (slot) { WebSocketClient.send(slot.command) } }}
              onContextMenu={(e) => { e.preventDefault(); setConfigOpen(i) }}
              title={slot ? `${slot.label} - ${slot.command}` : 'Right-click to configure'}
              className={`w-full aspect-square flex flex-col items-center justify-center rounded border text-[10px] gap-0
                ${slot
                  ? 'border-accent hover:bg-accent hover:text-white cursor-pointer'
                  : 'border-border opacity-40 cursor-default'
                }`}
            >
              {slot ? (
                <>
                  <span className="text-sm leading-none">{slot.emoji}</span>
                  <span className="text-text-secondary leading-none mt-0.5">{slot.label}</span>
                </>
              ) : (
                <span className="text-text-secondary text-xs">+</span>
              )}
            </button>
            {configOpen === i && (
              <SlotConfigPopover index={i} current={slot} onClose={() => setConfigOpen(null)} />
            )}
          </div>
        ))}
      </div>
    </PanelWrapper>
  )
}
