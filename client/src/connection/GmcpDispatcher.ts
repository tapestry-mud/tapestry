import { useCharStore } from '../stores/charStore'
import { useEquipmentStore } from '../stores/equipmentStore'
import { useDisplayStore } from '../stores/displayStore'
import { useInventoryStore } from '../stores/inventoryStore'
import { useXpStore } from '../stores/xpStore'
import { useCommandsStore } from '../stores/commandsStore'
import { useRoomStore } from '../stores/roomStore'
import { useChatStore } from '../stores/chatStore'
import { useDebugStore } from '../stores/debugStore'
import { useConnectionStore } from '../stores/connectionStore'
import { useNearbyStore } from '../stores/nearbyStore'
import { useWorldStore } from '../stores/worldStore'
import { useLayoutStore } from '../stores/layoutStore'
import { useAffectsStore } from '../stores/affectsStore'
import { useCombatTargetStore } from '../stores/combatTargetStore'
import { useCombatTargetsStore } from '../stores/combatTargetsStore'
import { getTerminal } from '../terminal/terminalStore'
import {
  CharVitalsSchema, CharStatusSchema, CharExperienceSchema, CharCommandsSchema,
  CharEffectsSchema, CharCombatTargetSchema, CharCombatTargetsSchema, CharItemsSchema,
  CharEquipmentSchema,
  RoomInfoSchema, RoomNearbySchema,
  WorldTimeSchema, WorldWeatherSchema, WorldDisplayColorsSchema, CommChannelSchema, LoginPhaseSchema,
} from '../types/gmcp'

type GmcpHandler = (data: unknown) => void

const handlers = new Map<string, GmcpHandler>()

export const GmcpDispatcher = {
  register(pkg: string, handler: GmcpHandler): void {
    handlers.set(pkg, handler)
  },

  dispatch(pkg: string, data: unknown): void {
    const handler = handlers.get(pkg)
    if (handler) {
      handler(data)
    } else if (import.meta.env.DEV) {
      console.debug(`[GMCP] unhandled package: ${pkg}`)
    }
  },

  clear(): void {
    handlers.clear()
  },
}

export function initCoreHandlers(): void {
  GmcpDispatcher.register('Char.Vitals', (data) => {
    const result = CharVitalsSchema.safeParse(data)
    if (result.success) {
      useCharStore.getState().updateVitals(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Vitals')
    }
  })

  GmcpDispatcher.register('Char.Status', (data) => {
    const result = CharStatusSchema.safeParse(data)
    if (result.success) {
      useCharStore.getState().updateStatus(result.data)
      useRoomStore.getState().loadMapForCharacter(result.data.name)
      useLayoutStore.getState().loadLayoutForCharacter(result.data.name)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Status')
    }
  })

  GmcpDispatcher.register('Char.Commands', (data) => {
    const result = CharCommandsSchema.safeParse(data)
    if (result.success) {
      useCommandsStore.getState().setCommands(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Commands')
    }
  })

  GmcpDispatcher.register('Char.StatusVars', (data) => {
    useDebugStore.getState().logGmcp('Char.StatusVars', data, 'in')
  })

  GmcpDispatcher.register('Char.Experience', (data) => {
    const result = CharExperienceSchema.safeParse(data)
    if (result.success) {
      useXpStore.getState().setTracks(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Experience')
    }
  })

  GmcpDispatcher.register('Char.Effects', (data) => {
    const result = CharEffectsSchema.safeParse(data)
    if (result.success) {
      useAffectsStore.getState().setAffects(
        result.data.effects.map((e) => ({
          id: e.id,
          name: e.name,
          duration: e.remainingPulses,
          type: e.type,
          flags: e.flags,
        }))
      )
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Effects')
    }
  })

  GmcpDispatcher.register('Char.Combat.Target', (data) => {
    const result = CharCombatTargetSchema.safeParse(data)
    if (result.success) {
      useCombatTargetStore.getState().update(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Combat.Target')
    }
  })

  GmcpDispatcher.register('Char.Combat.Targets', (data) => {
    const result = CharCombatTargetsSchema.safeParse(data)
    if (result.success) {
      useCombatTargetsStore.getState().update(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Combat.Targets')
    }
  })

  GmcpDispatcher.register('Room.Info', (data) => {
    const result = RoomInfoSchema.safeParse(data)
    if (result.success) {
      useRoomStore.getState().updateRoom(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Room.Info')
    }
  })

  GmcpDispatcher.register('Room.Nearby', (data) => {
    const result = RoomNearbySchema.safeParse(data)
    if (result.success) {
      useNearbyStore.getState().setEntities(result.data.entities)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Room.Nearby')
    }
  })

  GmcpDispatcher.register('Room.WrongDir', () => {
    const { lastDirection, removeExit } = useRoomStore.getState()
    if (lastDirection) { removeExit(lastDirection) }
  })

  GmcpDispatcher.register('World.Time', (data) => {
    const result = WorldTimeSchema.safeParse(data)
    if (result.success) {
      useWorldStore.getState().setTime(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'World.Time')
    }
  })

  GmcpDispatcher.register('World.Weather', (data) => {
    const result = WorldWeatherSchema.safeParse(data)
    if (result.success) {
      useWorldStore.getState().setWeather(result.data.state)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'World.Weather')
    }
  })

  GmcpDispatcher.register('World.Display.Colors', (data) => {
    const result = WorldDisplayColorsSchema.safeParse(data)
    if (result.success) {
      useDisplayStore.getState().setColorMap(result.data.colors)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'World.Display.Colors')
    }
  })

  GmcpDispatcher.register('Char.Items', (data) => {
    const result = CharItemsSchema.safeParse(data)
    if (result.success) {
      useInventoryStore.getState().setItems(result.data.items)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Items')
    }
  })

  GmcpDispatcher.register('Char.Equipment', (data) => {
    const result = CharEquipmentSchema.safeParse(data)
    if (result.success) {
      useEquipmentStore.getState().setEquipment(result.data.slots)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Equipment')
    }
  })

  GmcpDispatcher.register('Comm.Channel', (data) => {
    const result = CommChannelSchema.safeParse(data)
    if (result.success) {
      useChatStore.getState().addMessage(result.data)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Comm.Channel')
    }
  })

  GmcpDispatcher.register('Char.Login.Phase', (data) => {
    const result = LoginPhaseSchema.safeParse(data)
    if (result.success) {
      useConnectionStore.getState().setLoginPhase(result.data.phase)
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Login.Phase')
    }
  })
}
