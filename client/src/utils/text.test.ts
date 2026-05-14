import { describe, it, expect } from 'vitest'
import { stripMarkup } from './text'

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
