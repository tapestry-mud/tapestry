import { beforeEach, describe, it, expect, vi } from 'vitest'
import { buildContextAnnouncement, handleContextCommands } from './contextCommands'
import { useNearbyStore } from '../../stores/nearbyStore'
import { useAnnounceStore } from '../announceStore'
import type { Entity } from '../../types/game'

function mob(name: string, tags: string[], type: Entity['type'] = 'mob'): Entity {
  return { name, type, tags }
}

function player(name: string): Entity {
  return { name, type: 'player' }
}

describe('buildContextAnnouncement', () => {
  it('returns No one nearby for empty entity list', () => {
    expect(buildContextAnnouncement([])).toBe('No one nearby.')
  })

  it('formats single shop NPC', () => {
    expect(buildContextAnnouncement([mob('Grimjaw', ['shop'])])).toBe(
      'Shop: Grimjaw -- list, buy, sell, value.'
    )
  })

  it('formats multiple shop NPCs on one line', () => {
    expect(buildContextAnnouncement([
      mob('Grimjaw', ['shop']),
      mob('Thora', ['shop']),
    ])).toBe('Shop: Grimjaw, Thora -- list, buy, sell, value.')
  })

  it('formats single trainer NPC', () => {
    expect(buildContextAnnouncement([mob('Elara', ['skill_trainer'])])).toBe(
      'Trainer: Elara -- practice, train.'
    )
  })

  it('formats single quest NPC', () => {
    expect(buildContextAnnouncement([mob('Eldric', ['quest'])])).toBe('Quest: Eldric.')
  })

  it('formats single hostile', () => {
    expect(buildContextAnnouncement([mob('goblin scout', ['hostile'])])).toBe(
      'Hostiles: goblin scout.'
    )
  })

  it('counts multiple hostiles of same name', () => {
    expect(buildContextAnnouncement([
      mob('goblin scout', ['hostile']),
      mob('goblin scout', ['hostile']),
      mob('goblin scout', ['hostile']),
    ])).toBe('Hostiles: goblin scout times 3.')
  })

  it('lists different hostile names comma-separated', () => {
    expect(buildContextAnnouncement([
      mob('goblin scout', ['hostile']),
      mob('orc warrior', ['hostile']),
    ])).toBe('Hostiles: goblin scout, orc warrior.')
  })

  it('mixes counted and uncounted hostiles', () => {
    expect(buildContextAnnouncement([
      mob('goblin scout', ['hostile']),
      mob('goblin scout', ['hostile']),
      mob('orc warrior', ['hostile']),
    ])).toBe('Hostiles: goblin scout times 2, orc warrior.')
  })

  it('formats players', () => {
    expect(buildContextAnnouncement([player('Gandalf'), player('Aragorn')])).toBe(
      'Players: Gandalf, Aragorn.'
    )
  })

  it('formats untagged mobs as Creatures', () => {
    expect(buildContextAnnouncement([mob('old grey cat', [])])).toBe(
      'Creatures: old grey cat.'
    )
  })

  it('combines all types in order: shop, trainer, quest, hostile, player, creature', () => {
    expect(buildContextAnnouncement([
      mob('Grimjaw', ['shop']),
      mob('Elara', ['skill_trainer']),
      mob('Eldric', ['quest']),
      mob('goblin scout', ['hostile']),
      player('Gandalf'),
      mob('old grey cat', []),
    ])).toBe(
      'Shop: Grimjaw -- list, buy, sell, value. ' +
      'Trainer: Elara -- practice, train. ' +
      'Quest: Eldric. ' +
      'Hostiles: goblin scout. ' +
      'Players: Gandalf. ' +
      'Creatures: old grey cat.'
    )
  })
})

describe('handleContextCommands', () => {
  beforeEach(() => {
    useNearbyStore.setState({ entities: [] })
  })

  it('calls pushMessage assertive with entity announcement', () => {
    useNearbyStore.setState({ entities: [mob('Grimjaw', ['shop'])] })
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleContextCommands()
    expect(pushSpy).toHaveBeenCalledWith(
      'Shop: Grimjaw -- list, buy, sell, value.',
      'assertive'
    )
    pushSpy.mockRestore()
  })

  it('announces No one nearby when entities is empty', () => {
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleContextCommands()
    expect(pushSpy).toHaveBeenCalledWith('No one nearby.', 'assertive')
    pushSpy.mockRestore()
  })
})
