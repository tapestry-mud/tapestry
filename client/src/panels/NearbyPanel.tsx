import { useNearbyStore } from '../stores/nearbyStore'
import { PanelWrapper } from './PanelWrapper'
import { Badge } from '@/components/ui/Badge'
import type { Entity } from '../types/game'

interface EntityGroup {
  entity: Entity
  count: number
}

function groupEntities(entities: Entity[]): EntityGroup[] {
  const groups: EntityGroup[] = []
  const templateCounts = new Map<string, EntityGroup>()

  for (const entity of entities) {
    if (entity.type === 'player') {
      groups.push({ entity, count: 1 })
      continue
    }
    if (entity.templateId) {
      const stackKey = `${entity.templateId}:${entity.healthTier ?? 'perfect'}`
      const existing = templateCounts.get(stackKey)
      if (existing) {
        existing.count++
      } else {
        const group = { entity, count: 1 }
        templateCounts.set(stackKey, group)
        groups.push(group)
      }
    } else {
      groups.push({ entity, count: 1 })
    }
  }

  return groups
}

type BadgeVariant = 'player' | 'mob' | 'npc' | 'party'

interface EntityRole {
  variant: BadgeVariant
  label: string
}

function entityRole(entity: Entity): EntityRole {
  if (entity.type === 'player') { return { variant: 'player', label: 'PLAYER' } }
  const tags = entity.tags ?? []
  if (tags.includes('hostile') || tags.includes('killable')) { return { variant: 'mob', label: 'HOSTILE' } }
  if (tags.includes('shop')) { return { variant: 'npc', label: 'SHOP' } }
  if (tags.includes('skill_trainer')) { return { variant: 'npc', label: 'TRAINER' } }
  if (tags.includes('quest')) { return { variant: 'npc', label: 'QUEST' } }
  if (tags.includes('friendly')) { return { variant: 'npc', label: 'FRIENDLY' } }
  return { variant: 'npc', label: 'NPC' }
}

export function NearbyPanel() {
  const entities = useNearbyStore((s) => s.entities)
  const groups = groupEntities(entities)

  return (
    <PanelWrapper title="Nearby" count={entities.length}>
      <div className="font-mono text-xs">
        {groups.map(({ entity, count }, i) => {
          const role = entityRole(entity)
          return (
            <div key={i} className="flex items-center justify-between py-0.5">
              <span className="text-text-primary truncate">
                {count > 1 ? `${entity.name} x${count}` : entity.name}
                {entity.healthTier && entity.healthTier !== 'perfect' && (
                  <span className="text-text-secondary ml-1">({entity.healthTier})</span>
                )}
              </span>
              <Badge variant={role.variant} className="ml-2">
                {role.label}
              </Badge>
            </div>
          )
        })}
      </div>
    </PanelWrapper>
  )
}
