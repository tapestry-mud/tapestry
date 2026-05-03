import type { Entity } from '../../types/game'
import { useNearbyStore } from '../../stores/nearbyStore'
import { useAnnounceStore } from '../announceStore'

const TAG_ORDER = ['shop', 'skill_trainer', 'quest', 'hostile'] as const
type KnownTag = typeof TAG_ORDER[number]

function getEntityRole(entity: Entity): KnownTag | 'player' | 'creature' {
  if (entity.type === 'player') { return 'player' }
  for (const tag of TAG_ORDER) {
    if (entity.tags?.includes(tag)) { return tag }
  }
  return 'creature'
}

export function buildContextAnnouncement(entities: Entity[]): string {
  if (entities.length === 0) { return 'No one nearby.' }

  const shops: Entity[] = []
  const trainers: Entity[] = []
  const quests: Entity[] = []
  const hostiles: Entity[] = []
  const players: Entity[] = []
  const creatures: Entity[] = []

  for (const entity of entities) {
    const role = getEntityRole(entity)
    if (role === 'shop') { shops.push(entity) }
    else if (role === 'skill_trainer') { trainers.push(entity) }
    else if (role === 'quest') { quests.push(entity) }
    else if (role === 'hostile') { hostiles.push(entity) }
    else if (role === 'player') { players.push(entity) }
    else { creatures.push(entity) }
  }

  const parts: string[] = []

  if (shops.length > 0) {
    parts.push(`Shop: ${shops.map((e) => e.name).join(', ')} -- list, buy, sell, value.`)
  }
  if (trainers.length > 0) {
    parts.push(`Trainer: ${trainers.map((e) => e.name).join(', ')} -- practice, train.`)
  }
  if (quests.length > 0) {
    parts.push(`Quest: ${quests.map((e) => e.name).join(', ')}.`)
  }
  if (hostiles.length > 0) {
    const counts = new Map<string, number>()
    for (const e of hostiles) {
      counts.set(e.name, (counts.get(e.name) ?? 0) + 1)
    }
    const hostilesStr = Array.from(counts.entries())
      .map(([name, count]) => count > 1 ? `${name} times ${count}` : name)
      .join(', ')
    parts.push(`Hostiles: ${hostilesStr}.`)
  }
  if (players.length > 0) {
    parts.push(`Players: ${players.map((e) => e.name).join(', ')}.`)
  }
  if (creatures.length > 0) {
    parts.push(`Creatures: ${creatures.map((e) => e.name).join(', ')}.`)
  }

  return parts.join(' ')
}

export function handleContextCommands(): void {
  const { entities } = useNearbyStore.getState()
  const text = buildContextAnnouncement(entities)
  useAnnounceStore.getState().pushMessage(text, 'assertive')
}
