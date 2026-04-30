import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { Badge } from './Badge'

describe('Badge', () => {
  it('renders children', () => {
    render(<Badge>MOB</Badge>)
    expect(screen.getByText('MOB')).toBeTruthy()
  })

  it('applies mob variant classes', () => {
    const { container } = render(<Badge variant="mob">MOB</Badge>)
    const el = container.firstChild as HTMLElement
    expect(el.className).toContain('bg-red-900')
  })

  it('applies player variant classes', () => {
    const { container } = render(<Badge variant="player">PLAYER</Badge>)
    const el = container.firstChild as HTMLElement
    expect(el.className).toContain('border-blue-600')
  })

  it('applies npc variant classes', () => {
    const { container } = render(<Badge variant="npc">NPC</Badge>)
    const el = container.firstChild as HTMLElement
    expect(el.className).toContain('border-gray-600')
  })

  it('applies party variant classes', () => {
    const { container } = render(<Badge variant="party">PARTY</Badge>)
    const el = container.firstChild as HTMLElement
    expect(el.className).toContain('bg-indigo-800')
  })

  it('merges additional className', () => {
    const { container } = render(<Badge variant="mob" className="ml-2">MOB</Badge>)
    const el = container.firstChild as HTMLElement
    expect(el.className).toContain('ml-2')
  })
})
