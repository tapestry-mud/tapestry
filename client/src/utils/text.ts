export function stripMarkup(text: string): string {
    let result = text.replace(/\x1b\[[0-9;]*m/g, '')
    result = result.replace(/\[[0-9;]+m/g, '')
    result = result.replace(/\{[a-zA-Z0-9_]+\}/g, '')
    result = result.replace(/<[^>]+>/g, '')
    return result
}

const COLOR_TAG_CLASSES: Record<string, string> = {
    highlight: 'text-yellow-300 font-semibold',
    danger: 'text-red-400',
    subtle: 'text-gray-400 italic',
    item: 'text-blue-300',
    'item.common': 'text-gray-300',
    'item.uncommon': 'text-green-300',
    'item.rare': 'text-blue-300',
    'item.epic': 'text-purple-300',
    npc: 'text-green-400',
    player: 'text-cyan-300',
}

export interface ColorSegment {
    text: string
    className?: string
}

export function parseColorTags(text: string): ColorSegment[] {
    const parts: ColorSegment[] = []
    const re = /<([a-zA-Z0-9_.]+)>([\s\S]*?)<\/\1>|([^<]+)/g
    let match: RegExpExecArray | null

    while ((match = re.exec(text)) !== null) {
        if (match[3] !== undefined) {
            parts.push({ text: match[3] })
        } else {
            parts.push({ text: match[2], className: COLOR_TAG_CLASSES[match[1]] })
        }
    }

    return parts
}
