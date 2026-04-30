import { describe, it, expect } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { PanelWrapper } from './PanelWrapper'

describe('PanelWrapper', () => {
  it('renders title in header', () => {
    render(<PanelWrapper title="Affects"><div>content</div></PanelWrapper>)
    expect(screen.getByText('Affects')).toBeDefined()
  })

  it('shows children when not collapsed', () => {
    render(<PanelWrapper title="Test"><div>content</div></PanelWrapper>)
    expect(screen.getByText('content')).toBeDefined()
  })

  it('hides children when defaultCollapsed is true', () => {
    render(<PanelWrapper title="Test" defaultCollapsed><div>content</div></PanelWrapper>)
    expect(screen.queryByText('content')).toBeNull()
  })

  it('toggles children on chevron button click', () => {
    render(<PanelWrapper title="Test"><div>content</div></PanelWrapper>)
    expect(screen.getByText('content')).toBeDefined()
    fireEvent.click(screen.getByRole('button'))
    expect(screen.queryByText('content')).toBeNull()
    fireEvent.click(screen.getByRole('button'))
    expect(screen.getByText('content')).toBeDefined()
  })

  it('shows count badge when count prop provided', () => {
    render(<PanelWrapper title="Test" count={3}><div /></PanelWrapper>)
    expect(screen.getByText('3')).toBeDefined()
  })

  it('does not show badge when count is 0', () => {
    render(<PanelWrapper title="Test" count={0}><div /></PanelWrapper>)
    expect(screen.queryByText('0')).toBeNull()
  })

  it('content wrapper has animate-in class when open', () => {
    render(<PanelWrapper title="Test"><div>content</div></PanelWrapper>)
    const contentDiv = screen.getByText('content').parentElement!
    expect(contentDiv.className).toContain('animate-in')
  })
})
