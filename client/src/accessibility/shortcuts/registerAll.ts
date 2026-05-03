import { useShortcutStore } from '../../stores/shortcutStore'
import { handleRoomDescription } from './roomDescription'
import { handleContextCommands } from './contextCommands'

export function registerAllShortcuts(): void {
  const { register } = useShortcutStore.getState()
  register('room-description', 'Room description', 'Alt+L', handleRoomDescription)
  register('context-commands', 'Context commands', 'Alt+C', handleContextCommands)
}
