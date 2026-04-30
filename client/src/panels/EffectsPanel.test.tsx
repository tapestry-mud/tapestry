import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { EffectsPanel } from './EffectsPanel'
import { useAffectsStore } from '../stores/affectsStore'

beforeEach(() => {
  useAffectsStore.setState({
    affects: [],
    setAffects: useAffectsStore.getState().setAffects,
    tickTimers: useAffectsStore.getState().tickTimers,
  })
})

describe('EffectsPanel', () => {
  it('shows "No active effects" when list is empty', () => {
    render(<EffectsPanel />)
    expect(screen.getByText(/no active effects/i)).toBeTruthy()
  })

  it('shows effect name and duration', () => {
    useAffectsStore.setState({
      affects: [{ id: 'sanctuary', name: 'Sanctuary', duration: 48, type: 'buff', flags: [] }],
      setAffects: useAffectsStore.getState().setAffects,
      tickTimers: useAffectsStore.getState().tickTimers,
    })
    render(<EffectsPanel />)
    expect(screen.getByText('Sanctuary')).toBeTruthy()
    expect(screen.getByText(/48p/)).toBeTruthy()
  })

  it('shows multiple effects', () => {
    useAffectsStore.setState({
      affects: [
        { id: 'sanctuary', name: 'Sanctuary', duration: 48, type: 'buff', flags: [] },
        { id: 'bless', name: 'Bless', duration: 120, type: 'buff', flags: [] },
      ],
      setAffects: useAffectsStore.getState().setAffects,
      tickTimers: useAffectsStore.getState().tickTimers,
    })
    render(<EffectsPanel />)
    expect(screen.getByText('Sanctuary')).toBeTruthy()
    expect(screen.getByText('Bless')).toBeTruthy()
  })

  it('shows green circle for buffs and red circle for debuffs', () => {
    useAffectsStore.setState({
      affects: [
        { id: 'poison', name: 'Poison', duration: 30, type: 'debuff', flags: [] },
      ],
      setAffects: useAffectsStore.getState().setAffects,
      tickTimers: useAffectsStore.getState().tickTimers,
    })
    const { container } = render(<EffectsPanel />)
    expect(container.textContent).toContain('\u{1F534}') // red circle for debuff
  })

  it('renders "perm" for permanent effects with duration -1', () => {
    useAffectsStore.setState({
      affects: [{ id: 'bless', name: 'Bless', duration: -1, type: 'buff', flags: [] }],
      setAffects: useAffectsStore.getState().setAffects,
      tickTimers: useAffectsStore.getState().tickTimers,
    })
    render(<EffectsPanel />)
    expect(screen.getByText(/perm/)).toBeTruthy()
  })

  it('shows green circle emoji for buff type', () => {
    useAffectsStore.setState({
      affects: [{ id: 'sanctuary', name: 'Sanctuary', duration: 48, type: 'buff', flags: [] }],
      setAffects: useAffectsStore.getState().setAffects,
      tickTimers: useAffectsStore.getState().tickTimers,
    })
    const { container } = render(<EffectsPanel />)
    expect(container.textContent).toContain('\u{1F7E2}') // green circle for buff
  })
})
