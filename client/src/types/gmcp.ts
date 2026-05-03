import { z } from 'zod'

export const CharVitalsSchema = z.object({
  hp: z.number(),
  maxhp: z.number(),
  mana: z.number(),
  maxmana: z.number(),
  mv: z.number(),
  maxmv: z.number(),
})
export type CharVitals = z.infer<typeof CharVitalsSchema>

export const CharStatusSchema = z.object({
  name: z.string(),
  race: z.string(),
  class: z.string(),
  level: z.number(),
  str: z.number().nullish(),
  int: z.number().nullish(),
  wis: z.number().nullish(),
  dex: z.number().nullish(),
  con: z.number().nullish(),
  luk: z.number().nullish(),
  alignment: z.number().nullish(),
  alignmentBucket: z.string().nullish(),
  gold: z.number().nullish(),
  hungerTier: z.string().nullish(),
  hungerValue: z.number().nullish(),
  isAdmin: z.boolean().nullish(),
})
export type CharStatus = z.infer<typeof CharStatusSchema>

export const CharExperienceSchema = z.object({
  tracks: z.array(z.object({
    name: z.string(),
    level: z.number(),
    xp: z.number(),
    xpToNext: z.number(),
    currentLevelThreshold: z.number(),
  })),
})
export type CharExperience = z.infer<typeof CharExperienceSchema>

export const CharCommandsSchema = z.object({
  commands: z.array(z.object({
    keyword: z.string(),
    category: z.string(),
    description: z.string(),
    aliases: z.array(z.string()),
  })),
})
export type CharCommands = z.infer<typeof CharCommandsSchema>

export const CharEffectsSchema = z.object({
  effects: z.array(z.object({
    id: z.string(),
    name: z.string(),
    remainingPulses: z.number(),
    flags: z.array(z.string()),
    type: z.enum(['buff', 'debuff']),
  })),
})
export type CharEffects = z.infer<typeof CharEffectsSchema>

export const CharCombatTargetSchema = z.object({
  active: z.boolean(),
  name: z.string().optional(),
  healthTier: z.string().optional(),
  healthText: z.string().optional(),
})
export type CharCombatTarget = z.infer<typeof CharCombatTargetSchema>

export const CharCombatTargetsSchema = z.object({
  targets: z.array(z.object({
    id: z.string(),
    name: z.string(),
    healthTier: z.string(),
    healthText: z.string(),
    isPrimary: z.boolean(),
  })),
})
export type CharCombatTargets = z.infer<typeof CharCombatTargetsSchema>

export const CharStatusVarsSchema = z.record(z.string(), z.unknown())
export type CharStatusVars = z.infer<typeof CharStatusVarsSchema>

export const RoomInfoSchema = z.object({
  num: z.string(),
  name: z.string(),
  area: z.string(),
  environment: z.string(),
  exits: z.record(z.string(), z.string()),
  description: z.string().nullish(),
  weatherExposed: z.boolean().optional(),
  timeExposed: z.boolean().optional(),
  doors: z.record(z.string(), z.object({
    isClosed: z.boolean(),
    isLocked: z.boolean(),
  })).nullish(),
})
export type RoomInfo = z.infer<typeof RoomInfoSchema>

export const RoomNearbySchema = z.object({
  entities: z.array(z.object({
    name: z.string(),
    type: z.enum(['player', 'mob', 'npc']),
    templateId: z.string().optional(),
    tags: z.array(z.string()).optional(),
    healthTier: z.string().optional(),
  })),
})
export type RoomNearby = z.infer<typeof RoomNearbySchema>

export const WorldTimeSchema = z.object({
  hour: z.number(),
  period: z.enum(['dawn', 'day', 'dusk', 'night']),
  dayCount: z.number(),
})
export type WorldTime = z.infer<typeof WorldTimeSchema>

export const WorldWeatherSchema = z.object({ state: z.string() })
export type WorldWeather = z.infer<typeof WorldWeatherSchema>

export const WorldDisplayColorsSchema = z.object({
  colors: z.record(z.string(), z.string()),
})
export type WorldDisplayColors = z.infer<typeof WorldDisplayColorsSchema>

export const CharItemsSchema = z.object({
  items: z.array(z.object({
    id: z.string(),
    name: z.string(),
    templateId: z.string().optional(),
    quantity: z.number(),
    rarity: z.string().optional(),
    essence: z.string().optional(),
    rarityTag: z.string().optional(),
    essenceTag: z.string().optional(),
  })),
})
export type CharItems = z.infer<typeof CharItemsSchema>

export const CharEquipmentItemSchema = z.object({
  id: z.string(),
  name: z.string(),
  rarity: z.string().optional(),
  essence: z.string().optional(),
  rarityTag: z.string().optional(),
  essenceTag: z.string().optional(),
})

export const CharEquipmentSchema = z.object({
  slots: z.record(z.string(), CharEquipmentItemSchema.nullish()),
})
export type CharEquipment = z.infer<typeof CharEquipmentSchema>

export const CommChannelSchema = z.object({
  channel: z.string(),
  sender: z.string(),
  text: z.string(),
})
export type CommChannel = z.infer<typeof CommChannelSchema>

export const IncomingTextEnvelopeSchema = z.object({
  type: z.literal('text'),
  data: z.string(),
})

export const IncomingGmcpEnvelopeSchema = z.object({
  type: z.literal('gmcp'),
  package: z.string(),
  data: z.unknown(),
})

export const IncomingEnvelopeSchema = z.discriminatedUnion('type', [
  IncomingTextEnvelopeSchema,
  IncomingGmcpEnvelopeSchema,
])
export type IncomingEnvelope = z.infer<typeof IncomingEnvelopeSchema>

export const LoginPhaseSchema = z.object({
  phase: z.enum(['name', 'password', 'creating', 'playing']),
})
export type LoginPhase = z.infer<typeof LoginPhaseSchema>['phase']
export type LoginPhaseState = LoginPhase | 'disconnected'

export const LoginPromptSchema = z.object({
  prompt: z.string(),
})
export type LoginPrompt = z.infer<typeof LoginPromptSchema>

export const FlowStepSchema = z.object({
  type: z.enum(['info', 'choice', 'text', 'confirm']),
  prompt: z.string(),
  options: z.array(z.object({
    label: z.string(),
    tagLine: z.string().optional(),
  })).optional(),
})
export type FlowStep = z.infer<typeof FlowStepSchema>

export const FlowHelpSchema = z.object({
  text: z.string(),
})
export type FlowHelp = z.infer<typeof FlowHelpSchema>
