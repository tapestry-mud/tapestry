import type { AnsiToken, AnsiStyles } from '../types/game'

type ParserState = 'TEXT' | 'ESCAPE' | 'CSI'

const COLOR_MAP: Record<number, string> = {
  30: 'text-ansi-black',   31: 'text-ansi-red',
  32: 'text-ansi-green',   33: 'text-ansi-yellow',
  34: 'text-ansi-blue',    35: 'text-ansi-magenta',
  36: 'text-ansi-cyan',    37: 'text-ansi-white',
  90: 'text-ansi-bright-black',  91: 'text-ansi-bright-red',
  92: 'text-ansi-bright-green',  93: 'text-ansi-bright-yellow',
  94: 'text-ansi-bright-blue',   95: 'text-ansi-bright-magenta',
  96: 'text-ansi-bright-cyan',   97: 'text-ansi-bright-white',
}

function applyAttr(styles: AnsiStyles, n: number): AnsiStyles {
  if (n === 0) { return {} }
  if (n === 1) { return { ...styles, bold: true } }
  if (COLOR_MAP[n] !== undefined) { return { ...styles, fg: COLOR_MAP[n] } }
  return styles
}

export function parseAnsi(input: string): AnsiToken[] {
  const tokens: AnsiToken[] = []
  let state: ParserState = 'TEXT'
  let text = ''
  let csi = ''
  let styles: AnsiStyles = {}

  function flush(): void {
    if (text.length > 0) {
      tokens.push({ text, styles: { ...styles } })
      text = ''
    }
  }

  function applyCsi(): void {
    if (csi.length === 0) { return }
    const term = csi[csi.length - 1]
    if (term !== 'm') { return }
    const params = csi.slice(0, -1)
    if (params === '' || params === '0') {
      styles = {}
      return
    }
    for (const part of params.split(';')) {
      const n = parseInt(part, 10)
      if (!isNaN(n)) { styles = applyAttr(styles, n) }
    }
  }

  for (let i = 0; i < input.length; i++) {
    const ch = input[i]
    switch (state) {
      case 'TEXT':
        if (ch === '\x1b') { flush(); state = 'ESCAPE' }
        else if (ch >= ' ' || ch === '\t') { text += ch }
        break
      case 'ESCAPE':
        if (ch === '[') {
          state = 'CSI'
        } else {
          state = 'TEXT'
        }
        csi = ''
        break
      case 'CSI':
        csi += ch
        if (ch >= '@' && ch <= '~') { applyCsi(); csi = ''; state = 'TEXT' }
        else if (!(ch >= ' ' && ch <= '?')) { csi = ''; state = 'TEXT' }
        break
    }
  }

  flush()
  return tokens
}
