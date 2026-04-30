import { describe, it, expect, vi, beforeEach } from 'vitest'
import { PackRegistry } from './registry'
import { GmcpDispatcher } from '../connection/GmcpDispatcher'
import { useHotbarStore } from '../stores/hotbarStore'
import type { PackManifest } from '../types/pack'

beforeEach(() => {
  GmcpDispatcher.clear()
  useHotbarStore.setState({ slots: Array(10).fill(null) })
  PackRegistry.clear()
})

describe('PackRegistry', () => {
  it('registers GMCP handlers from manifest', () => {
    const handler = vi.fn()
    const manifest: PackManifest = {
      name: 'test-pack',
      gmcpHandlers: { 'Custom.Event': handler },
      panels: [],
    }
    PackRegistry.register(manifest)
    GmcpDispatcher.dispatch('Custom.Event', { data: 1 })
    expect(handler).toHaveBeenCalledWith({ data: 1 })
  })

  it('populates hotbar defaults for empty slots', () => {
    const manifest: PackManifest = {
      name: 'test-pack',
      gmcpHandlers: {},
      panels: [],
      hotbarDefaults: [{ index: 0, emoji: '⚔️', label: 'atk', command: 'attack' }],
    }
    PackRegistry.register(manifest)
    expect(useHotbarStore.getState().slots[0]).toEqual({ emoji: '⚔️', label: 'atk', command: 'attack' })
  })

  it('does not overwrite existing hotbar slot with default', () => {
    useHotbarStore.getState().setSlot(0, { emoji: '🛡️', label: 'def', command: 'defend' })
    const manifest: PackManifest = {
      name: 'test-pack',
      gmcpHandlers: {},
      panels: [],
      hotbarDefaults: [{ index: 0, emoji: '⚔️', label: 'atk', command: 'attack' }],
    }
    PackRegistry.register(manifest)
    expect(useHotbarStore.getState().slots[0]?.command).toBe('defend')
  })

  it('returns registered panels from getPanels', () => {
    const Comp = () => null
    const manifest: PackManifest = {
      name: 'test-pack',
      gmcpHandlers: {},
      panels: [{ id: 'custom', component: Comp, zone: 'sidebar-top', order: 1 }],
    }
    PackRegistry.register(manifest)
    expect(PackRegistry.getPanels('sidebar-top')).toHaveLength(1)
    expect(PackRegistry.getPanels('sidebar-top')[0].id).toBe('custom')
  })
})
