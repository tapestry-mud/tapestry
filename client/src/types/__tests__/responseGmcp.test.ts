// client/src/types/__tests__/responseGmcp.test.ts
import { describe, it, expect } from 'vitest'
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
} from '../responseGmcp'

describe('ResponseFeedbackSchema', () => {
  it('parses a valid feedback event', () => {
    const result = ResponseFeedbackSchema.safeParse({
      status: 'ok',
      type: 'info',
      message: 'You pick up the sword.',
      category: 'interaction',
    })
    expect(result.success).toBe(true)
  })

  it('allows missing category', () => {
    const result = ResponseFeedbackSchema.safeParse({
      status: 'ok',
      type: 'success',
      message: 'Done.',
    })
    expect(result.success).toBe(true)
  })
})

describe('ResponseShopListSchema', () => {
  it('parses a shop list with items', () => {
    const result = ResponseShopListSchema.safeParse({
      status: 'ok',
      shopkeeper: 'Old Bob',
      items: [{ id: 'sword-template', name: 'Iron Sword', price: 100 }],
    })
    expect(result.success).toBe(true)
  })

  it('items require only id, name, price', () => {
    const result = ResponseShopListSchema.safeParse({
      status: 'ok',
      shopkeeper: 'Bob',
      items: [{ id: 'x', name: 'Thing', price: 5 }],
    })
    expect(result.success).toBe(true)
  })
})

describe('ResponseTrainingPracticeSchema', () => {
  it('parses a practice list', () => {
    const result = ResponseTrainingPracticeSchema.safeParse({
      status: 'ok',
      trainer: 'Master Runn',
      trainerTier: 'master',
      abilities: [{ id: 'slash', name: 'Slash', proficiency: 45, cap: 75, nextTier: 'master' }],
    })
    expect(result.success).toBe(true)
  })

  it('allows null trainer when no trainer in room', () => {
    const result = ResponseTrainingPracticeSchema.safeParse({
      status: 'ok',
      trainer: null,
      trainerTier: null,
      abilities: [],
    })
    expect(result.success).toBe(true)
  })
})

describe('ResponseCharScoreSchema', () => {
  it('parses a full score payload', () => {
    const payload = {
      status: 'ok',
      name: 'Zara',
      race: 'Elf',
      class: 'Mage',
      level: 10,
      stats: { str: 12, int: 18, wis: 14, dex: 16, con: 10, luk: 8 },
      hp: 80, maxHp: 100,
      mana: 150, maxMana: 200,
      mv: 100, maxMv: 100,
      gold: 500,
      alignment: '-200 [neutral]',
      hungerTier: 'full',
      xpTracks: [{ name: 'adventurer', level: 10, xp: 5000, xpToNext: 2000 }],
    }
    const result = ResponseCharScoreSchema.safeParse(payload)
    expect(result.success).toBe(true)
  })
})
