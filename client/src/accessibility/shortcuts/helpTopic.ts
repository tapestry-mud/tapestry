import { useHelpStore } from '../../stores/helpStore'
import { stripMarkup } from '../../utils/text'

export function handleHelpTopic(): void {
  const { response, isOpen, pushAnnouncement } = useHelpStore.getState()
  if (!isOpen || !response) { return }

  if (response.status === 'ok') {
    const topic = response.topic
    const parts: string[] = []
    if (topic.syntax.length > 0) { parts.push('Syntax: ' + topic.syntax.join(', ')) }
    parts.push(topic.body)
    if (topic.seeAlso.length > 0) { parts.push('See also: ' + topic.seeAlso.join(', ')) }
    pushAnnouncement(stripMarkup(parts.join('. ')))
  } else if (response.status === 'multiple') {
    const titles = response.matches.map((m) => m.title).join(', ')
    const label = response.term
      ? `Multiple matches for "${response.term}": ${titles}. Type help [topic] for details.`
      : `Help categories: ${titles}. Type help [category] to browse.`
    pushAnnouncement(label)
  }
}
