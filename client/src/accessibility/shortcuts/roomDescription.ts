import { useRoomStore } from '../../stores/roomStore'
import { useAnnounceStore } from '../announceStore'
import { stripMarkup } from '../../utils/text'

export { stripMarkup }

export function handleRoomDescription(): void {
  const { current } = useRoomStore.getState()
  const exits = Object.keys(current.exits)
  const exitStr = exits.length > 0 ? `Exits: ${exits.join(', ')}.` : 'No exits.'
  const desc = stripMarkup(current.description).replace(/\.$/, '')
  const name = stripMarkup(current.name)
  const text = `${name}. ${desc}. ${exitStr}`
  useAnnounceStore.getState().pushMessage(text, 'assertive')
}
