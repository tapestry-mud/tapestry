import { useShortcutStore } from '../../stores/shortcutStore'
import { handleRoomDescription } from './roomDescription'
import { handleContextCommands } from './contextCommands'
import { handleHelpTopic } from './helpTopic'

export function registerAllShortcuts(): void {
  const { register } = useShortcutStore.getState()
  register('room-description', 'Room description', 'Alt+L', handleRoomDescription)
  register('context-commands', 'Context commands', 'Alt+C', handleContextCommands)
  register('help-topic', 'Read help topic', 'Alt+H', handleHelpTopic)
}
