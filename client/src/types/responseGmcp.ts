// client/src/types/responseGmcp.ts
import { z } from 'zod'

// --- Response.Feedback ---

export const ResponseFeedbackSchema = z.object({
  status: z.enum(['ok', 'error']),
  type: z.enum(['success', 'failure', 'info']),
  message: z.string(),
  category: z.string().optional(),
})

export type ResponseFeedback = z.infer<typeof ResponseFeedbackSchema>

// --- Response.Shop.List ---

export const ShopItemSchema = z.object({
  id: z.string(),
  name: z.string(),
  price: z.number(),
  rarity: z.string().nullable().optional(),
  tags: z.array(z.string()).optional(),
})

export const ResponseShopListSchema = z.object({
  status: z.enum(['ok', 'error']),
  shopkeeper: z.string(),
  items: z.array(ShopItemSchema),
  filter: z.string().optional(),
})

export type ResponseShopList = z.infer<typeof ResponseShopListSchema>

// --- Response.Shop.Buy ---

export const ResponseShopBuySchema = z.object({
  status: z.enum(['ok', 'error']),
  message: z.string(),
  item: z.string().optional(),
  cost: z.number().optional(),
  goldRemaining: z.number().optional(),
})

export type ResponseShopBuy = z.infer<typeof ResponseShopBuySchema>

// --- Response.Shop.Sell ---

export const ResponseShopSellSchema = z.object({
  status: z.enum(['ok', 'error']),
  message: z.string(),
  item: z.string().optional(),
  earnings: z.number().optional(),
  goldRemaining: z.number().optional(),
})

export type ResponseShopSell = z.infer<typeof ResponseShopSellSchema>

// --- Response.Shop.Value ---

export const ResponseShopValueSchema = z.object({
  status: z.enum(['ok', 'error']),
  message: z.string(),
  item: z.string().optional(),
  buyPrice: z.number().optional(),
  sellPrice: z.number().optional(),
})

export type ResponseShopValue = z.infer<typeof ResponseShopValueSchema>

// --- Response.Training.Practice ---

export const PracticeAbilitySchema = z.object({
  id: z.string(),
  name: z.string(),
  proficiency: z.number(),
  cap: z.number(),
  nextTier: z.string().nullable().optional(),
})

export const ResponseTrainingPracticeSchema = z.object({
  status: z.enum(['ok', 'error']),
  message: z.string().optional(),
  trainer: z.string().nullable().optional(),
  trainerTier: z.string().nullable().optional(),
  abilities: z.array(PracticeAbilitySchema),
})

export type ResponseTrainingPractice = z.infer<typeof ResponseTrainingPracticeSchema>

// --- Response.Training.Train ---

export const ResponseTrainingTrainSchema = z.object({
  status: z.enum(['ok', 'error']),
  message: z.string().optional(),
  trainsRemaining: z.number().nullable().optional(),
  stats: z.record(z.string(), z.number()).optional(),
})

export type ResponseTrainingTrain = z.infer<typeof ResponseTrainingTrainSchema>

// --- Response.Char.Score ---

export const ResponseCharScoreSchema = z.object({
  status: z.literal('ok'),
  name: z.string(),
  race: z.string(),
  class: z.string(),
  level: z.number(),
  stats: z.record(z.string(), z.number()),
  hp: z.number(),
  maxHp: z.number(),
  mana: z.number(),
  maxMana: z.number(),
  mv: z.number(),
  maxMv: z.number(),
  gold: z.number(),
  alignment: z.string(),
  hungerTier: z.string(),
  xpTracks: z.array(z.object({
    name: z.string(),
    level: z.number(),
    xp: z.number(),
    xpToNext: z.number(),
  })),
})

export type ResponseCharScore = z.infer<typeof ResponseCharScoreSchema>

// --- Response.Look ---

export const LookEntitySchema = z.object({
  name: z.string(),
  type: z.string(),
  tags: z.array(z.string()).optional(),
})

export const LookItemSchema = z.object({
  name: z.string(),
  quantity: z.number(),
})

export const ResponseLookSchema = z.object({
  status: z.literal('ok'),
  type: z.enum(['room', 'entity', 'item']),
  name: z.string(),
  description: z.string(),
  exits: z.array(z.string()).optional(),
  entities: z.array(LookEntitySchema).optional(),
  items: z.array(LookItemSchema).optional(),
  healthTier: z.string().optional(),
  equipment: z.record(z.string(), z.string()).optional(),
  rarity: z.string().optional(),
  stats: z.record(z.string(), z.unknown()).optional(),
})

export type ResponseLook = z.infer<typeof ResponseLookSchema>

// --- Response.Help ---

export const ResponseHelpSchema = z.object({
  status: z.enum(['ok', 'error']),
  topic: z.string(),
  category: z.string().optional(),
  body: z.string(),
  seeAlso: z.array(z.string()).optional(),
})

export type ResponseHelp = z.infer<typeof ResponseHelpSchema>
