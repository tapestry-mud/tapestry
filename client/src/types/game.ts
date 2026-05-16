export interface AnsiStyles {
  fg?: string
  bold?: boolean
}

export interface AnsiToken {
  text: string
  styles: AnsiStyles
}

export interface RoomNode {
  num: string
  name: string
  x: number
  y: number
  z: number
  exits: Record<string, string>
  exitNames: Record<string, string>
}

export interface ChatMessage {
  id: string
  channel: string
  sender: string
  text: string
  timestamp: number
}

export interface Affect {
  id: string
  name: string
  duration: number
  type: 'buff' | 'debuff'
  flags: string[]
}

export interface Entity {
  name: string
  type: 'player' | 'npc'
  templateId?: string
  tags?: string[]
  healthTier?: string
  disposition?: 'Neutral' | 'Friendly' | 'Hostile'
  roles?: string[]
  keywords?: string[]
}

export interface Item {
  id: string
  name: string
  quantity: number
  icon?: string
  templateId?: string
  rarity?: string
  essence?: string
  rarityTag?: string
  essenceTag?: string
}

export interface HotbarSlot {
  emoji: string
  label: string
  command: string
}
