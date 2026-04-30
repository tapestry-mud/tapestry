export interface TagSegment {
  text: string
  htmlClass?: string
}

const TAG_RE = /<([\w.]+)>([\s\S]*?)<\/\1>/g

export function renderTags(raw: string, colorMap: Record<string, string>): TagSegment[] {
  if (!raw) { return [] }
  const segments: TagSegment[] = []
  let last = 0
  let match: RegExpExecArray | null
  TAG_RE.lastIndex = 0
  while ((match = TAG_RE.exec(raw)) !== null) {
    if (match.index > last) {
      segments.push({ text: raw.slice(last, match.index) })
    }
    segments.push({ text: match[2], htmlClass: colorMap[match[1]] })
    last = match.index + match[0].length
  }
  if (last < raw.length) {
    segments.push({ text: raw.slice(last) })
  }
  return segments
}
