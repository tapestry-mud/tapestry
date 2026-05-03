import { describe, it, expect } from 'vitest'
import { buildContextHint } from './contextHint'
import type { Entity } from '../../types/game'

function mob(name: string, tags: string[]): Entity {
  return { name, type: 'mob', tags }
}

function player(name: string): Entity {
  return { name, type: 'player' }
}

describe('buildContextHint', () => {
  it('returns empty string for empty entity list', () => {
    expect(buildContextHint([])).toBe('')
  })

  it('returns empty string for players only', () => {
    expect(buildContextHint([player('Gandalf')])).toBe('')
  })

  it('returns empty string for untagged mobs', () => {
    expect(buildContextHint([mob('old grey cat', [])])).toBe('')
  })

  it('shop only', () => {
    expect(buildContextHint([mob('Grimjaw', ['shop'])])).toBe('Shop nearby.')
  })

  it('skill_trainer only', () => {
    expect(buildContextHint([mob('Elara', ['skill_trainer'])])).toBe('Trainer nearby.')
  })

  it('quest only', () => {
    expect(buildContextHint([mob('Eldric', ['quest'])])).toBe('Quest nearby.')
  })

  it('hostile only', () => {
    expect(buildContextHint([mob('goblin scout', ['hostile'])])).toBe('Hostiles nearby.')
  })

  it('shop and trainer', () => {
    expect(buildContextHint([
      mob('Grimjaw', ['shop']),
      mob('Elara', ['skill_trainer']),
    ])).toBe('Shop and trainer nearby.')
  })

  it('three types comma-separated with and', () => {
    expect(buildContextHint([
      mob('Grimjaw', ['shop']),
      mob('Elara', ['skill_trainer']),
      mob('Eldric', ['quest']),
    ])).toBe('Shop, trainer, and quest nearby.')
  })

  it('all four types', () => {
    expect(buildContextHint([
      mob('Grimjaw', ['shop']),
      mob('Elara', ['skill_trainer']),
      mob('Eldric', ['quest']),
      mob('goblin scout', ['hostile']),
    ])).toBe('Shop, trainer, quest, and hostiles nearby.')
  })

  it('ignores player when mixed with shop', () => {
    expect(buildContextHint([
      mob('Grimjaw', ['shop']),
      player('Gandalf'),
    ])).toBe('Shop nearby.')
  })
})
