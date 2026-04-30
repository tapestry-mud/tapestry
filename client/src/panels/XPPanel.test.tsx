import { beforeEach, describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { XPPanel } from './XPPanel'
import { useXpStore } from '../stores/xpStore'

beforeEach(() => { useXpStore.setState({ tracks: [] }) })

describe('XPPanel', () => {
  it('shows experience label', () => {
    render(<XPPanel />)
    expect(screen.getByText(/experience/i)).toBeTruthy()
  })

  it('shows no-data message when tracks are empty', () => {
    render(<XPPanel />)
    expect(screen.getByText(/no progression data/i)).toBeTruthy()
  })

  it('shows track name and level when tracks exist', () => {
    useXpStore.setState({
      tracks: [{ name: 'core', level: 5, xp: 450, xpToNext: 550, currentLevelThreshold: 0 }],
    })
    render(<XPPanel />)
    expect(screen.getByText(/core/i)).toBeTruthy()
    expect(screen.getByText(/lvl 5/i)).toBeTruthy()
  })

  it('shows correct percentage for a track', () => {
    useXpStore.setState({
      tracks: [{ name: 'core', level: 5, xp: 450, xpToNext: 550, currentLevelThreshold: 0 }],
    })
    render(<XPPanel />)
    expect(screen.getByText(/45%/)).toBeTruthy()
  })

  it('shows xp progress numbers', () => {
    useXpStore.setState({
      tracks: [{ name: 'core', level: 5, xp: 450, xpToNext: 550, currentLevelThreshold: 0 }],
    })
    render(<XPPanel />)
    expect(screen.getByText(/450/)).toBeTruthy()
    expect(screen.getByText(/1,000/)).toBeTruthy()
  })

  it('XP bar has correct width', () => {
    useXpStore.setState({
      tracks: [{ name: 'core', level: 5, xp: 450, xpToNext: 550, currentLevelThreshold: 0 }],
    })
    const { container } = render(<XPPanel />)
    const bar = container.querySelector('[data-bar="xp-core"]')
    expect(bar).not.toBeNull()
    expect(bar!.getAttribute('style')).toContain('45%')
  })

  it('shows 100% when xpToNext is 0 (max level)', () => {
    useXpStore.setState({
      tracks: [{ name: 'core', level: 50, xp: 9999, xpToNext: 0, currentLevelThreshold: 9000 }],
    })
    render(<XPPanel />)
    expect(screen.getByText(/100%/)).toBeTruthy()
  })
})
