import { describe, it, expect } from 'vitest'
import { parseAnsi } from './AnsiParser'

describe('parseAnsi - plain text', () => {
  it('returns empty array for empty string', () => {
    expect(parseAnsi('')).toEqual([])
  })

  it('returns single token for plain text', () => {
    expect(parseAnsi('Hello world')).toEqual([{ text: 'Hello world', styles: {} }])
  })

  it('drops null bytes', () => {
    expect(parseAnsi('ab\x00cd')[0].text).toBe('abcd')
  })

  it('drops CR and LF', () => {
    expect(parseAnsi('line\r\n')[0].text).toBe('line')
  })
})

describe('parseAnsi - foreground colors', () => {
  it.each([
    [30, 'text-ansi-black'],   [31, 'text-ansi-red'],
    [32, 'text-ansi-green'],   [33, 'text-ansi-yellow'],
    [34, 'text-ansi-blue'],    [35, 'text-ansi-magenta'],
    [36, 'text-ansi-cyan'],    [37, 'text-ansi-white'],
    [90, 'text-ansi-bright-black'], [91, 'text-ansi-bright-red'],
    [92, 'text-ansi-bright-green'], [93, 'text-ansi-bright-yellow'],
    [94, 'text-ansi-bright-blue'],  [95, 'text-ansi-bright-magenta'],
    [96, 'text-ansi-bright-cyan'],  [97, 'text-ansi-bright-white'],
  ])('code %i maps to %s', (code, expected) => {
    const tokens = parseAnsi(`\x1b[${code}mtext\x1b[0m`)
    expect(tokens[0].styles.fg).toBe(expected)
  })
})

describe('parseAnsi - bold', () => {
  it('applies bold (\\x1b[1m)', () => {
    expect(parseAnsi('\x1b[1mtext\x1b[0m')[0].styles.bold).toBe(true)
  })
})

describe('parseAnsi - reset', () => {
  it('resets with \\x1b[0m', () => {
    const tokens = parseAnsi('\x1b[31mred\x1b[0mplain')
    expect(tokens[0].styles.fg).toBe('text-ansi-red')
    expect(tokens[1].styles).toEqual({})
  })

  it('resets with bare \\x1b[m', () => {
    const tokens = parseAnsi('\x1b[31mred\x1b[mplain')
    expect(tokens[0].styles.fg).toBe('text-ansi-red')
    expect(tokens[1].styles).toEqual({})
  })
})

describe('parseAnsi - compound sequences', () => {
  it('parses bold+red from \\x1b[1;31m', () => {
    const tokens = parseAnsi('\x1b[1;31mtext\x1b[0m')
    expect(tokens[0].styles).toEqual({ bold: true, fg: 'text-ansi-red' })
  })

  it('parses multiple styled segments', () => {
    const tokens = parseAnsi('\x1b[31mred\x1b[32mgreen\x1b[0mplain')
    expect(tokens).toHaveLength(3)
    expect(tokens[0]).toEqual({ text: 'red', styles: { fg: 'text-ansi-red' } })
    expect(tokens[1]).toEqual({ text: 'green', styles: { fg: 'text-ansi-green' } })
    expect(tokens[2]).toEqual({ text: 'plain', styles: {} })
  })
})

describe('parseAnsi - adversarial input', () => {
  it('passes <script> through as plain text', () => {
    const tokens = parseAnsi('<script>alert(1)</script>')
    expect(tokens).toHaveLength(1)
    expect(tokens[0].text).toBe('<script>alert(1)</script>')
    expect(tokens[0].styles).toEqual({})
  })

  it('passes HTML entities through as plain text', () => {
    expect(parseAnsi('&amp; &lt; &gt;')[0].text).toBe('&amp; &lt; &gt;')
  })

  it('drops lone ESC followed by non-[', () => {
    const tokens = parseAnsi('ab\x1bcd')
    expect(tokens[0].text).toBe('ab')
    expect(tokens[1].text).toBe('d')
  })

  it('drops partial CSI with no terminator', () => {
    expect(parseAnsi('\x1b[31')).toEqual([])
  })

  it('drops screen clear sequence, keeps surrounding text', () => {
    const tokens = parseAnsi('\x1b[2Jtext')
    expect(tokens[0].text).toBe('text')
  })

  it('ignores unknown SGR attributes, does not crash', () => {
    const tokens = parseAnsi('\x1b[999mtext\x1b[0m')
    expect(tokens[0].styles).toEqual({})
    expect(tokens[0].text).toBe('text')
  })
})

describe('parseAnsi - performance', () => {
  it('parses 10,000 styled lines under 500ms', () => {
    const line = '\x1b[1;31mThe goblin \x1b[0msnarls at \x1b[33myou\x1b[0m.'
    const input = Array.from({ length: 10_000 }, () => line).join('\n')
    const start = performance.now()
    parseAnsi(input)
    expect(performance.now() - start).toBeLessThan(500)
  })
})
