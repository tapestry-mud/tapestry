import { announce } from '../accessibility/announceStore'
import { checkVitalAlerts } from '../accessibility/vitalAlerts'
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
import {
  CharVitalsSchema, CharStatusSchema, CharExperienceSchema, CharCommandsSchema,
  CharEffectsSchema, CharCombatTargetSchema, CharCombatTargetsSchema, CharItemsSchema,
  CharEquipmentSchema,
  RoomInfoSchema, RoomNearbySchema,
  WorldTimeSchema, WorldWeatherSchema, WorldDisplayColorsSchema, CommChannelSchema, LoginPhaseSchema,
  LoginPromptSchema, FlowStepSchema,
} from '../types/gmcp'
import {
  ResponseFeedbackSchema,
  ResponseShopListSchema,
  ResponseShopBuySchema,
  ResponseShopSellSchema,
  ResponseShopValueSchema,
  ResponseTrainingPracticeSchema,
  ResponseTrainingTrainSchema,
  ResponseCharScoreSchema,
  ResponseLookSchema,
  ResponseHelpSchema,
} from '../types/responseGmcp'

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
      return
    }

    // Unknown Response.* packages fall through to the feedback handler.
    // This handles new server-side response types without a client update,
    // as long as the payload carries a message field.
    if (pkg.startsWith('Response.')) {
      const feedbackHandler = handlers.get('Response.Feedback')
      if (feedbackHandler) {
        const asObj = data as Record<string, unknown>
        if (asObj && typeof asObj.message === 'string') {
          feedbackHandler({ status: 'ok', type: 'info', message: asObj.message, category: 'general' })
        }
      }
      return
    }

    if (import.meta.env.DEV) {
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
      const s = useCharStore.getState()
      checkVitalAlerts({ hp: s.hp, maxHp: s.maxHp, mana: s.mana, maxMana: s.maxMana, mv: s.mv, maxMv: s.maxMv })
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
      const prev = useCombatTargetStore.getState()
      useCombatTargetStore.getState().update(result.data)
      if (result.data.active && result.data.healthTier && result.data.healthTier !== prev.healthTier) {
        announce(`${result.data.name}: ${result.data.healthText ?? result.data.healthTier}`, 'combat', 'polite')
      }
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Combat.Target')
    }
  })

  GmcpDispatcher.register('Char.Combat.Targets', (data) => {
    const result = CharCombatTargetsSchema.safeParse(data)
    if (result.success) {
      const prev = useCombatTargetsStore.getState().targets
      useCombatTargetsStore.getState().update(result.data)
      if (prev.length === 0 && result.data.targets.length > 0) {
        announce(`Combat started with ${result.data.targets.length} target${result.data.targets.length > 1 ? 's' : ''}`, 'combat', 'assertive')
      } else if (prev.length > 0 && result.data.targets.length === 0) {
        announce('Combat ended', 'combat', 'assertive')
      }
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Char.Combat.Targets')
    }
  })

  GmcpDispatcher.register('Room.Info', (data) => {
    const result = RoomInfoSchema.safeParse(data)
    if (result.success) {
      useRoomStore.getState().updateRoom(result.data)
      const exits = Object.keys(result.data.exits)
      const exitStr = exits.length > 0 ? `, exits: ${exits.join(', ')}` : ', no exits'
      announce(`${result.data.name}${exitStr}`, 'room')
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
      announce(`${result.data.sender} on ${result.data.channel}: ${result.data.text}`, 'chat')
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

  GmcpDispatcher.register('Login.Prompt', (data) => {
    const result = LoginPromptSchema.safeParse(data)
    if (result.success) {
      announce(result.data.prompt, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Login.Prompt')
    }
  })

  GmcpDispatcher.register('Flow.Step', (data) => {
    const result = FlowStepSchema.safeParse(data)
    if (result.success) {
      const { type, prompt, options } = result.data
      announce(prompt, 'feedback')
      if (type === 'choice' && options && options.length > 0) {
        const optionText = options
          .map((o, i) => o.tagLine ? `${i + 1}. ${o.label}: ${o.tagLine}` : `${i + 1}. ${o.label}`)
          .join(', ')
        announce(optionText, 'feedback')
      }
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Flow.Step')
    }
  })

  // --- Response.Feedback: catch-all for unsuppressed command output ---
  GmcpDispatcher.register('Response.Feedback', (data) => {
    const result = ResponseFeedbackSchema.safeParse(data)
    if (result.success) {
      announce(result.data.message, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Feedback')
    }
  })

  // --- Response.Shop.List ---
  GmcpDispatcher.register('Response.Shop.List', (data) => {
    const result = ResponseShopListSchema.safeParse(data)
    if (result.success) {
      const count = result.data.items.length
      const summary = count === 0
        ? `${result.data.shopkeeper} has nothing for sale.`
        : `${result.data.shopkeeper} sells ${count} item${count === 1 ? '' : 's'}.`
      announce(summary, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Shop.List')
    }
  })

  // --- Response.Shop.Buy ---
  GmcpDispatcher.register('Response.Shop.Buy', (data) => {
    const result = ResponseShopBuySchema.safeParse(data)
    if (result.success) {
      announce(result.data.message, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Shop.Buy')
    }
  })

  // --- Response.Shop.Sell ---
  GmcpDispatcher.register('Response.Shop.Sell', (data) => {
    const result = ResponseShopSellSchema.safeParse(data)
    if (result.success) {
      announce(result.data.message, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Shop.Sell')
    }
  })

  // --- Response.Shop.Value ---
  GmcpDispatcher.register('Response.Shop.Value', (data) => {
    const result = ResponseShopValueSchema.safeParse(data)
    if (result.success) {
      announce(result.data.message, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Shop.Value')
    }
  })

  // --- Response.Training.Practice ---
  GmcpDispatcher.register('Response.Training.Practice', (data) => {
    const result = ResponseTrainingPracticeSchema.safeParse(data)
    if (result.success) {
      const { abilities, trainer } = result.data
      if (abilities.length === 0) {
        announce('No abilities to practice.', 'feedback')
      } else {
        const names = abilities.slice(0, 3).map((a) => a.name).join(', ')
        const more = abilities.length > 3 ? ` and ${abilities.length - 3} more` : ''
        const trainerNote = trainer ? ` ${trainer} is here.` : ''
        announce(`Practice list: ${names}${more}.${trainerNote}`, 'feedback')
      }
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Training.Practice')
    }
  })

  // --- Response.Training.Train ---
  GmcpDispatcher.register('Response.Training.Train', (data) => {
    const result = ResponseTrainingTrainSchema.safeParse(data)
    if (result.success) {
      if (result.data.message) {
        announce(result.data.message, 'feedback')
      } else if (result.data.trainsRemaining != null) {
        announce(`Trains available: ${result.data.trainsRemaining}.`, 'feedback')
      }
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Training.Train')
    }
  })

  // --- Response.Char.Score ---
  GmcpDispatcher.register('Response.Char.Score', (data) => {
    const result = ResponseCharScoreSchema.safeParse(data)
    if (result.success) {
      const d = result.data
      const summary = `${d.name}, level ${d.level} ${d.race} ${d.class}. HP: ${d.hp}/${d.maxHp}, Mana: ${d.mana}/${d.maxMana}, Gold: ${d.gold}.`
      announce(summary, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Char.Score')
    }
  })

  // --- Response.Look ---
  GmcpDispatcher.register('Response.Look', (data) => {
    const result = ResponseLookSchema.safeParse(data)
    if (result.success) {
      const d = result.data
      if (d.type === 'room') {
        const exits = d.exits && d.exits.length > 0
          ? `, exits: ${d.exits.join(', ')}`
          : ', no exits'
        const entityNames = d.entities && d.entities.length > 0
          ? `. ${d.entities.map((e) => e.name).join(', ')} here.`
          : ''
        announce(`${d.name}${exits}${entityNames}`, 'feedback')
      } else {
        announce(`${d.name}: ${d.description}`, 'feedback')
      }
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Look')
    }
  })

  // --- Response.Help ---
  GmcpDispatcher.register('Response.Help', (data) => {
    const result = ResponseHelpSchema.safeParse(data)
    if (result.success) {
      announce(`Help: ${result.data.body}`, 'feedback')
    } else {
      useDebugStore.getState().logConnection('gmcp-parse-error', 'Response.Help')
    }
  })
}
