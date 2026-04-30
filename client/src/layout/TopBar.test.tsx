import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { TopBar } from './TopBar'
import { useRoomStore } from '../stores/roomStore'

beforeEach(() => {
  useRoomStore.setState({
    current: { num: 1, name: 'Town Square', area: 'Midgaard', environment: 'city', exits: {} },
    mapGraph: new Map(),
    lastDirection: null,
  })
})

describe('TopBar', () => {
  it('shows game name', () => {
    render(<TopBar />)
    expect(screen.getByText('TAPESTRY')).toBeDefined()
  })

  it('shows area name from roomStore', () => {
    render(<TopBar />)
    expect(screen.getByText(/Midgaard/)).toBeDefined()
  })

  it('shows currency stub', () => {
    render(<TopBar />)
    expect(screen.getByText(/1,247/)).toBeDefined()
  })

  it('shows no area bracket when area is empty', () => {
    useRoomStore.setState({
      current: { num: 0, name: '', area: '', environment: '', exits: {} },
      mapGraph: new Map(),
      lastDirection: null,
    })
    render(<TopBar />)
    expect(screen.getByText('TAPESTRY')).toBeDefined()
    // Area span doesn't render when area is empty string (falsy)
    const container = screen.getByText('TAPESTRY').closest('div')
    const areaSpan = container?.querySelector('span:first-of-type')
    expect(areaSpan?.textContent).not.toContain('[')
  })
})
