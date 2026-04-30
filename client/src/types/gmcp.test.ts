import { describe, it, expect } from 'vitest'
import {
  CharVitalsSchema,
  CharStatusSchema,
  CharItemsSchema,
  CharEquipmentSchema,
  RoomInfoSchema,
  CommChannelSchema,
  IncomingEnvelopeSchema,
  LoginPhaseSchema,
  RoomNearbySchema,
  WorldTimeSchema,
  WorldWeatherSchema,
  WorldDisplayColorsSchema,
} from './gmcp'

describe('CharVitalsSchema', () => {
  it('parses valid vitals', () => {
    const result = CharVitalsSchema.safeParse({
      hp: 340, maxhp: 340, mana: 120, maxmana: 150, mv: 200, maxmv: 200,
    })
    expect(result.success).toBe(true)
  })

  it('rejects missing hp', () => {
    const result = CharVitalsSchema.safeParse({
      maxhp: 340, mana: 120, maxmana: 150, mv: 200, maxmv: 200,
    })
    expect(result.success).toBe(false)
  })

  it('rejects string hp', () => {
    const result = CharVitalsSchema.safeParse({
      hp: '340', maxhp: 340, mana: 120, maxmana: 150, mv: 200, maxmv: 200,
    })
    expect(result.success).toBe(false)
  })
})

describe('CharStatusSchema', () => {
  it('parses valid status', () => {
    const result = CharStatusSchema.safeParse({
      name: 'Raegar', race: 'Human', class: 'Warrior', level: 10,
    })
    expect(result.success).toBe(true)
  })

  it('rejects missing class', () => {
    const result = CharStatusSchema.safeParse({ name: 'Raegar', race: 'Human', level: 10 })
    expect(result.success).toBe(false)
  })
})

describe('RoomInfoSchema', () => {
  it('parses valid room with exits', () => {
    const result = RoomInfoSchema.safeParse({
      num: 'core:town-square', name: 'Town Square', area: 'Midgaard',
      environment: 'city', exits: { north: 'core:inn', east: 'core:store' },
    })
    expect(result.success).toBe(true)
  })

  it('parses room with empty exits', () => {
    const result = RoomInfoSchema.safeParse({
      num: 'core:room', name: 'A Room', area: 'Test', environment: 'inside', exits: {},
    })
    expect(result.success).toBe(true)
  })
})

describe('CommChannelSchema', () => {
  it('parses valid channel message', () => {
    const result = CommChannelSchema.safeParse({
      channel: 'chat', sender: 'Raegar', text: 'Hello world',
    })
    expect(result.success).toBe(true)
  })

  it('rejects missing sender', () => {
    const result = CommChannelSchema.safeParse({ channel: 'chat', text: 'Hello' })
    expect(result.success).toBe(false)
  })
})

describe('IncomingEnvelopeSchema', () => {
  it('parses text envelope', () => {
    const result = IncomingEnvelopeSchema.safeParse({
      type: 'text', data: 'A goblin is here.\r\n',
    })
    expect(result.success).toBe(true)
  })

  it('parses gmcp envelope', () => {
    const result = IncomingEnvelopeSchema.safeParse({
      type: 'gmcp', package: 'Char.Vitals', data: { hp: 100 },
    })
    expect(result.success).toBe(true)
  })

  it('rejects unknown envelope type', () => {
    const result = IncomingEnvelopeSchema.safeParse({ type: 'unknown', data: 'x' })
    expect(result.success).toBe(false)
  })
})

describe('LoginPhaseSchema', () => {
  it('accepts valid server phases', () => {
    for (const phase of ['name', 'password', 'creating', 'playing'] as const) {
      const result = LoginPhaseSchema.safeParse({ phase })
      expect(result.success, `phase=${phase}`).toBe(true)
    }
  })

  it('rejects disconnected (client-only value)', () => {
    const result = LoginPhaseSchema.safeParse({ phase: 'disconnected' })
    expect(result.success).toBe(false)
  })

  it('rejects unknown phase strings', () => {
    const result = LoginPhaseSchema.safeParse({ phase: 'lobby' })
    expect(result.success).toBe(false)
  })

  it('rejects missing phase field', () => {
    const result = LoginPhaseSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})

describe('RoomInfoSchema -- new optional fields', () => {
  it('accepts room with doors, description, weatherExposed, timeExposed', () => {
    const result = RoomInfoSchema.safeParse({
      num: 'core:cave', name: 'Cave', area: 'Wilds', environment: 'cave',
      exits: { north: 'core:tunnel' },
      description: 'A damp cave.',
      weatherExposed: false,
      timeExposed: false,
      doors: { north: { isClosed: true, isLocked: false } },
    })
    expect(result.success).toBe(true)
  })

  it('accepts room with no optional fields (backwards compat)', () => {
    const result = RoomInfoSchema.safeParse({
      num: 'core:room', name: 'A Room', area: 'Test', environment: 'inside', exits: {},
    })
    expect(result.success).toBe(true)
  })
})

describe('RoomNearbySchema', () => {
  it('parses entities list', () => {
    const result = RoomNearbySchema.safeParse({
      entities: [
        { name: 'Goblin', type: 'mob', templateId: 'goblin_basic' },
        { name: 'Kracus', type: 'player' },
      ],
    })
    expect(result.success).toBe(true)
  })

  it('rejects invalid type', () => {
    const result = RoomNearbySchema.safeParse({
      entities: [{ name: 'X', type: 'dragon' }],
    })
    expect(result.success).toBe(false)
  })
})

describe('WorldTimeSchema', () => {
  it('parses valid time packet', () => {
    const result = WorldTimeSchema.safeParse({ hour: 14, period: 'day', dayCount: 42 })
    expect(result.success).toBe(true)
  })

  it('rejects unknown period', () => {
    const result = WorldTimeSchema.safeParse({ hour: 6, period: 'morning', dayCount: 1 })
    expect(result.success).toBe(false)
  })
})

describe('WorldWeatherSchema', () => {
  it('parses state string', () => {
    const result = WorldWeatherSchema.safeParse({ state: 'rain' })
    expect(result.success).toBe(true)
  })
})

describe('CharItemsSchema', () => {
  it('parses valid items payload', () => {
    const result = CharItemsSchema.safeParse({
      items: [
        { id: 'abc', name: 'an iron sword', quantity: 1 },
        { id: 'def', name: 'a cure potion', templateId: 'core:cure-light', quantity: 3, rarity: 'common' },
      ],
    })
    expect(result.success).toBe(true)
  })

  it('accepts empty items array', () => {
    const result = CharItemsSchema.safeParse({ items: [] })
    expect(result.success).toBe(true)
  })

  it('rejects missing items field', () => {
    const result = CharItemsSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})

describe('CharEquipmentSchema', () => {
  it('parses valid equipment payload with items in slots', () => {
    const result = CharEquipmentSchema.safeParse({
      slots: {
        head: { id: 'abc123', name: 'a leather helm' },
        'finger:0': { id: 'def456', name: 'a ring of power', rarity: 'rare' },
      },
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.slots['head']?.name).toBe('a leather helm')
    }
  })

  it('accepts empty slots', () => {
    const result = CharEquipmentSchema.safeParse({ slots: {} })
    expect(result.success).toBe(true)
  })

  it('rejects missing slots field', () => {
    const result = CharEquipmentSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})

describe('WorldDisplayColorsSchema', () => {
  it('parses valid display colors payload', () => {
    const result = WorldDisplayColorsSchema.safeParse({
      colors: { 'item.rare': 'text-green-400', 'npc': 'text-yellow-400' },
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.colors['item.rare']).toBe('text-green-400')
    }
  })

  it('accepts empty colors object', () => {
    const result = WorldDisplayColorsSchema.safeParse({ colors: {} })
    expect(result.success).toBe(true)
  })

  it('rejects missing colors field', () => {
    const result = WorldDisplayColorsSchema.safeParse({})
    expect(result.success).toBe(false)
  })
})
