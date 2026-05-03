import type { Entity } from '../../types/game'

const TAG_SEQUENCE: Array<{ tag: string; label: string }> = [
  { tag: 'shop', label: 'shop' },
  { tag: 'skill_trainer', label: 'trainer' },
  { tag: 'quest', label: 'quest' },
  { tag: 'hostile', label: 'hostiles' },
]

export function buildContextHint(entities: Entity[]): string {
  const present = new Set<string>()
  for (const entity of entities) {
    if (entity.type === 'player') { continue }
    for (const { tag } of TAG_SEQUENCE) {
      if (entity.tags?.includes(tag)) {
        present.add(tag)
        break
      }
    }
  }

  if (present.size === 0) { return '' }

  const labels = TAG_SEQUENCE
    .filter(({ tag }) => present.has(tag))
    .map(({ label }) => label)

  labels[0] = labels[0].charAt(0).toUpperCase() + labels[0].slice(1)

  if (labels.length === 1) { return `${labels[0]} nearby.` }
  if (labels.length === 2) { return `${labels[0]} and ${labels[1]} nearby.` }

  const last = labels.pop()!
  return `${labels.join(', ')}, and ${last} nearby.`
}
