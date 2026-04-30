import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { VitalsBar } from './VitalsBar'
import { useCharStore } from '../stores/charStore'
import { useRoomStore } from '../stores/roomStore'

beforeEach(() => {
  useCharStore.setState({ name: 'Raegar', race: 'Human', class: 'Warrior', level: 10, hp: 340, maxHp: 340, mana: 120, maxMana: 150, mv: 200, maxMv: 200 })
  useRoomStore.setState({ current: { num: 1, name: 'Town Square', area: 'Midgaard', environment: 'city', exits: {} }, mapGraph: new Map(), lastDirection: null })
})

describe('VitalsBar', () => {
  it('displays character name', () => {
    render(<VitalsBar />)
    expect(screen.getByText('Raegar')).toBeDefined()
  })

  it('displays class', () => {
    render(<VitalsBar />)
    expect(screen.getByText(/Warrior/)).toBeDefined()
  })

  it('displays level', () => {
    render(<VitalsBar />)
    expect(screen.getByText(/10/)).toBeDefined()
  })

  it('displays room name', () => {
    render(<VitalsBar />)
    expect(screen.getByText('Town Square')).toBeDefined()
  })

  it('displays area name', () => {
    render(<VitalsBar />)
    expect(screen.getByText(/Midgaard/)).toBeDefined()
  })

  it('renders HP bar with correct width percentage', () => {
    useCharStore.setState({ name: 'R', race: 'H', class: 'W', level: 1, hp: 170, maxHp: 340, mana: 0, maxMana: 1, mv: 0, maxMv: 1 })
    const { container } = render(<VitalsBar />)
    const hpFill = container.querySelector('[data-bar="hp"]')
    expect(hpFill?.getAttribute('style')).toContain('50%')
  })
})
