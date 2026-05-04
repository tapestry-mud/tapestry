import { useRoomStore } from '../../stores/roomStore'
import { useAnnounceStore } from '../announceStore'

export function stripMarkup(text: string): string {
  // Remove ANSI escape sequences: \x1b[...m or ESC[...m
  let result = text.replace(/\x1b\[[0-9;]*m/g, '')
  // Remove bare bracket-form ANSI sequences that slipped through: [0m, [1;32m, etc.
  result = result.replace(/\[[0-9;]+m/g, '')
  // Remove Tapestry color/style tags: {cyan}, {bold}, {reset}, etc.
  result = result.replace(/\{[a-zA-Z0-9_]+\}/g, '')
  // Remove any remaining HTML-like tags
  result = result.replace(/<[^>]+>/g, '')
  return result
}

export function handleRoomDescription(): void {
  const { current } = useRoomStore.getState()
  const exits = Object.keys(current.exits)
  const exitStr = exits.length > 0 ? `Exits: ${exits.join(', ')}.` : 'No exits.'
  const desc = stripMarkup(current.description).replace(/\.$/, '')
  const name = stripMarkup(current.name)
  const text = `${name}. ${desc}. ${exitStr}`
  useAnnounceStore.getState().pushMessage(text, 'assertive')
}
