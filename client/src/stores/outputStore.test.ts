import { beforeEach, describe, it, expect } from 'vitest'
import { useOutputStore } from './outputStore'
import type { AnsiToken } from '../types/game'

const line = (t: string): AnsiToken[] => [{ text: t, styles: {} }]

beforeEach(() => {
  useOutputStore.setState({ lines: [], scrollLocked: false })
})

describe('outputStore', () => {
  it('starts with empty lines', () => {
    expect(useOutputStore.getState().lines).toHaveLength(0)
  })

  it('appendLine adds a parsed line', () => {
    useOutputStore.getState().appendLine(line('hello'))
    expect(useOutputStore.getState().lines[0][0].text).toBe('hello')
  })

  it('appendSystemMessage adds a plain text line', () => {
    useOutputStore.getState().appendSystemMessage("You can't go that way.")
    expect(useOutputStore.getState().lines[0][0].text).toBe("You can't go that way.")
  })

  it('clear removes all lines', () => {
    useOutputStore.getState().appendLine(line('hello'))
    useOutputStore.getState().clear()
    expect(useOutputStore.getState().lines).toHaveLength(0)
  })

  it('caps at 5000 lines, dropping oldest', () => {
    const store = useOutputStore.getState()
    for (let i = 0; i < 5005; i++) { store.appendLine(line(`${i}`)) }
    const { lines } = useOutputStore.getState()
    expect(lines).toHaveLength(5000)
    expect(lines[0][0].text).toBe('5')
  })

  it('setScrollLocked updates scrollLocked', () => {
    useOutputStore.getState().setScrollLocked(true)
    expect(useOutputStore.getState().scrollLocked).toBe(true)
  })
})
