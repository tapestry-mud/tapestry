import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { CombatStatsPanel } from './CombatStatsPanel'

describe('CombatStatsPanel', () => {
  it('renders the COMBAT section header', () => {
    render(<CombatStatsPanel />)
    expect(screen.getByText(/combat/i)).toBeTruthy()
  })

  it('shows hit and dam values', () => {
    render(<CombatStatsPanel />)
    expect(screen.getByText(/\+7/)).toBeTruthy()
    expect(screen.getByText(/\+5/)).toBeTruthy()
  })

  it('shows AC value', () => {
    render(<CombatStatsPanel />)
    expect(screen.getByText(/-42/)).toBeTruthy()
  })

  it('shows speed value', () => {
    render(<CombatStatsPanel />)
    expect(screen.getByText(/3\.2s/)).toBeTruthy()
  })
})
