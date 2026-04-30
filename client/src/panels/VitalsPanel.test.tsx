import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { VitalsPanel } from './VitalsPanel'
import { useCharStore } from '../stores/charStore'

beforeEach(() => {
  useCharStore.setState({
    name: 'MALLEK', race: 'Human', class: 'Warrior', level: 12,
    hp: 340, maxHp: 340, mana: 120, maxMana: 150, mv: 200, maxMv: 200,
    updateVitals: useCharStore.getState().updateVitals,
    updateStatus: useCharStore.getState().updateStatus,
  })
})

describe('VitalsPanel', () => {
  it('shows HP current and max', () => {
    render(<VitalsPanel />)
    expect(screen.getByText(/340 \/ 340/)).toBeTruthy()
  })

  it('shows mana current and max', () => {
    render(<VitalsPanel />)
    expect(screen.getByText(/120 \/ 150/)).toBeTruthy()
  })

  it('shows MV current and max', () => {
    render(<VitalsPanel />)
    expect(screen.getByText(/200 \/ 200/)).toBeTruthy()
  })

  it('HP bar width reflects 50% when half health', () => {
    useCharStore.setState({
      name: 'T', race: 'H', class: 'W', level: 1,
      hp: 170, maxHp: 340, mana: 0, maxMana: 1, mv: 0, maxMv: 1,
      updateVitals: useCharStore.getState().updateVitals,
      updateStatus: useCharStore.getState().updateStatus,
    })
    const { container } = render(<VitalsPanel />)
    const hpBar = container.querySelector('[data-bar="hp"]')
    expect(hpBar?.getAttribute('style')).toContain('50%')
  })

  it('mana bar width reflects 80% when 120 of 150', () => {
    const { container } = render(<VitalsPanel />)
    const manaBar = container.querySelector('[data-bar="mana"]')
    expect(manaBar?.getAttribute('style')).toContain('80%')
  })

  it('HP bar renders at 0% width when max is 0', () => {
    useCharStore.setState({
      name: 'T', race: 'H', class: 'W', level: 1,
      hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0,
      updateVitals: useCharStore.getState().updateVitals,
      updateStatus: useCharStore.getState().updateStatus,
    })
    const { container } = render(<VitalsPanel />)
    const hpBar = container.querySelector('[data-bar="hp"]')
    expect(hpBar?.getAttribute('style')).toContain('0%')
  })
})
