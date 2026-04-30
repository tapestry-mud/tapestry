import { beforeEach, describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { StatsPanel } from './StatsPanel'
import { useCharStore } from '../stores/charStore'

beforeEach(() => {
  useCharStore.setState({
    str: 0, int: 0, wis: 0, dex: 0, con: 0, luk: 0,
  })
})

describe('StatsPanel', () => {
  it('shows Stats title', () => {
    render(<StatsPanel />)
    expect(screen.getByText(/stats/i)).toBeTruthy()
  })

  it('shows all six stat labels', () => {
    render(<StatsPanel />)
    expect(screen.getByText('STR')).toBeTruthy()
    expect(screen.getByText('INT')).toBeTruthy()
    expect(screen.getByText('WIS')).toBeTruthy()
    expect(screen.getByText('DEX')).toBeTruthy()
    expect(screen.getByText('CON')).toBeTruthy()
    expect(screen.getByText('LUK')).toBeTruthy()
  })

  it('shows stat values from charStore', () => {
    useCharStore.setState({ str: 15, int: 12, wis: 10, dex: 14, con: 13, luk: 9 })
    render(<StatsPanel />)
    expect(screen.getByText('15')).toBeTruthy()
    expect(screen.getByText('12')).toBeTruthy()
    expect(screen.getByText('10')).toBeTruthy()
    expect(screen.getByText('14')).toBeTruthy()
    expect(screen.getByText('13')).toBeTruthy()
    expect(screen.getByText('9')).toBeTruthy()
  })

  it('shows dash when stat is 0', () => {
    const { getAllByText } = render(<StatsPanel />)
    expect(getAllByText('-').length).toBe(6)
  })
})
