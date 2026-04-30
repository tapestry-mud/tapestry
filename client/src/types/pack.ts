export interface PackPanel {
  id: string
  component: React.ComponentType
  zone: 'sidebar-top' | 'sidebar-bottom'
  order: number
}

export interface HotbarSlotDefault {
  index: number
  emoji: string
  label: string
  command: string
}

export interface PackManifest {
  name: string
  gmcpHandlers: Record<string, (data: unknown) => void>
  panels: PackPanel[]
  hotbarDefaults?: HotbarSlotDefault[]
}
