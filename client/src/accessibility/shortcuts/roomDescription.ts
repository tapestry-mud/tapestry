import { useRoomStore } from '../../stores/roomStore'
import { useAnnounceStore } from '../announceStore'

export function handleRoomDescription(): void {
  const { current } = useRoomStore.getState()
  const exits = Object.keys(current.exits)
  const exitStr = exits.length > 0 ? `Exits: ${exits.join(', ')}.` : 'No exits.'
  const desc = current.description.replace(/\.$/, '')
  const text = `${current.name}. ${desc}. ${exitStr}`
  useAnnounceStore.getState().pushMessage(text, 'assertive')
}
