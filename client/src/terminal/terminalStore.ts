import type { Terminal } from '@xterm/xterm'

let terminalInstance: Terminal | null = null

export function setTerminal(t: Terminal | null): void {
  terminalInstance = t
}

export function getTerminal(): Terminal | null {
  return terminalInstance
}
