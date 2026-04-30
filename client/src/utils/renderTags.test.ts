import { describe, it, expect } from 'vitest'
import { renderTags } from './renderTags'

describe('renderTags', () => {
  it('returns plain text as single segment with no class', () => {
    const result = renderTags('hello world', {})
    expect(result).toEqual([{ text: 'hello world' }])
  })

  it('applies class for known semantic tag', () => {
    const colorMap = { 'item.rare': 'text-green-400' }
    const result = renderTags('a <item.rare>sword</item.rare> here', colorMap)
    expect(result).toEqual([
      { text: 'a ' },
      { text: 'sword', htmlClass: 'text-green-400' },
      { text: ' here' },
    ])
  })

  it('applies undefined class for unknown tag', () => {
    const result = renderTags('<unknown>text</unknown>', {})
    expect(result).toEqual([{ text: 'text', htmlClass: undefined }])
  })

  it('handles multiple tags in one string', () => {
    const colorMap = { npc: 'text-yellow-400', direction: 'text-cyan-400' }
    const result = renderTags('<npc>Bob</npc> goes <direction>north</direction>', colorMap)
    expect(result).toHaveLength(3)
    expect(result[0]).toEqual({ text: 'Bob', htmlClass: 'text-yellow-400' })
    expect(result[1]).toEqual({ text: ' goes ' })
    expect(result[2]).toEqual({ text: 'north', htmlClass: 'text-cyan-400' })
  })

  it('returns empty array for empty string', () => {
    const result = renderTags('', {})
    expect(result).toEqual([])
  })

  it('handles text with no tags as single segment', () => {
    const result = renderTags('The goblin attacks you.', { npc: 'text-yellow-400' })
    expect(result).toEqual([{ text: 'The goblin attacks you.' }])
  })

  it('produces correct results on repeated calls', () => {
    const map = { npc: 'text-yellow-400' }
    const first = renderTags('<npc>Alice</npc>', map)
    const second = renderTags('<npc>Bob</npc>', map)
    expect(first[0].text).toBe('Alice')
    expect(second[0].text).toBe('Bob')
  })
})
