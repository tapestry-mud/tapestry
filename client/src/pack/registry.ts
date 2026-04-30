import { GmcpDispatcher } from '../connection/GmcpDispatcher'
import { useHotbarStore } from '../stores/hotbarStore'
import type { PackManifest, PackPanel } from '../types/pack'

const panels: PackPanel[] = []

export const PackRegistry = {
  register(manifest: PackManifest): void {
    for (const [pkg, handler] of Object.entries(manifest.gmcpHandlers)) {
      GmcpDispatcher.register(pkg, handler)
    }

    for (const panel of manifest.panels) {
      if (!panels.find((p) => p.id === panel.id)) {
        panels.push(panel)
      }
    }
    panels.sort((a, b) => a.order - b.order)

    if (manifest.hotbarDefaults) {
      const { slots, setSlot } = useHotbarStore.getState()
      for (const def of manifest.hotbarDefaults) {
        if (def.index >= 0 && def.index < slots.length && slots[def.index] === null) {
          setSlot(def.index, { emoji: def.emoji, label: def.label, command: def.command })
        }
      }
    }
  },

  getPanels(zone: PackPanel['zone']): PackPanel[] {
    return panels.filter((p) => p.zone === zone)
  },

  clear(): void {
    panels.length = 0
  },
}
