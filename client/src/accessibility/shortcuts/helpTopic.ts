import { useHelpStore } from '../../stores/helpStore'
import { useAnnounceStore } from '../announceStore'
import { stripMarkup } from '../../utils/text'

export function handleHelpTopic(): void {
  const { response, isOpen } = useHelpStore.getState()
  if (!isOpen || !response) { return }

  if (response.status === 'ok') {
    const topic = response.topic
    const parts: string[] = [topic.brief]
    if (topic.syntax.length > 0) { parts.push('Syntax: ' + topic.syntax.join(', ')) }
    if (topic.seeAlso.length > 0) { parts.push('See also: ' + topic.seeAlso.join(', ')) }
    parts.push(topic.body)
    useAnnounceStore.getState().pushMessage(stripMarkup(parts.join('. ')), 'assertive')
  } else if (response.status === 'multiple') {
    const titles = response.matches.map((m) => m.title).join(', ')
    const label = response.term
      ? `Multiple matches for "${response.term}": ${titles}. Type help [topic] for details.`
      : `Help categories: ${titles}. Type help [category] to browse.`
    useAnnounceStore.getState().pushMessage(label, 'assertive')
  }
}
