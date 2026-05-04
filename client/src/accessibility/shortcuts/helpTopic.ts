import { useHelpStore } from '../../stores/helpStore'
import { useAnnounceStore } from '../announceStore'
import { stripMarkup } from '../../utils/text'

export function handleHelpTopic(): void {
  const { response, isOpen } = useHelpStore.getState()
  if (!isOpen || !response || response.status !== 'ok') { return }
  const topic = response.topic
  const parts: string[] = [topic.brief]
  for (const s of topic.syntax) {
    parts.push('Syntax: ' + s)
  }
  if (topic.seeAlso.length > 0) { parts.push('See also: ' + topic.seeAlso.join(', ')) }
  parts.push(topic.body)
  useAnnounceStore.getState().pushMessage(stripMarkup(parts.join('. ')), 'assertive')
}
