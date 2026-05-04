import { describe, it, expect } from 'vitest'
import { stripMarkup, parseColorTags } from './text'

describe('stripMarkup', () => {
    it('strips ANSI escape sequences', () => {
        expect(stripMarkup('\x1b[32mgreen\x1b[0m')).toBe('green')
    })

    it('strips bracket-form ANSI', () => {
        expect(stripMarkup('[32mtext[0m')).toBe('text')
    })

    it('strips Tapestry brace color tags', () => {
        expect(stripMarkup('{cyan}text{reset}')).toBe('text')
    })

    it('strips semantic angle-bracket tags', () => {
        expect(stripMarkup('<highlight>text</highlight>')).toBe('text')
    })

    it('passes plain text through unchanged', () => {
        expect(stripMarkup('plain text')).toBe('plain text')
    })
})

describe('parseColorTags', () => {
    it('returns single plain segment for plain text', () => {
        const parts = parseColorTags('hello')
        expect(parts).toHaveLength(1)
        expect(parts[0].text).toBe('hello')
        expect(parts[0].className).toBeUndefined()
    })

    it('returns tagged segment with className for known tag', () => {
        const parts = parseColorTags('<highlight>bright</highlight>')
        expect(parts).toHaveLength(1)
        expect(parts[0].text).toBe('bright')
        expect(parts[0].className).toContain('text-yellow')
    })

    it('handles mixed tagged and plain text', () => {
        const parts = parseColorTags('before <highlight>mid</highlight> after')
        expect(parts).toHaveLength(3)
        expect(parts[0].text).toBe('before ')
        expect(parts[1].className).toBeTruthy()
        expect(parts[2].text).toBe(' after')
    })
})
