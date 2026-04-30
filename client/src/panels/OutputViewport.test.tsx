import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render } from '@testing-library/react'
import { OutputViewport } from './OutputViewport'

const mocks = vi.hoisted(() => {
  const mockOpen = vi.fn()
  const mockLoadAddon = vi.fn()
  const mockDispose = vi.fn()
  const mockWrite = vi.fn()
  const mockFit = vi.fn()
  // Must use function (not arrow) so the mock can be called with `new`
  const MockTerminal = vi.fn(function(this: Record<string, unknown>) {
    this.open = mockOpen
    this.loadAddon = mockLoadAddon
    this.dispose = mockDispose
    this.write = mockWrite
    this.onScroll = vi.fn()
    this.buffer = { active: { viewportY: 0, length: 0 } }
    this.rows = 24
  })
  const MockFitAddon = vi.fn(function(this: Record<string, unknown>) {
    this.fit = mockFit
  })
  return { MockTerminal, mockOpen, mockLoadAddon, mockDispose, mockFit, MockFitAddon, mockWrite }
})

vi.mock('@xterm/xterm', () => ({ Terminal: mocks.MockTerminal }))
vi.mock('@xterm/addon-fit', () => ({ FitAddon: mocks.MockFitAddon }))

beforeEach(() => {
  vi.clearAllMocks()
})

describe('OutputViewport', () => {
  it('renders the container without crashing', () => {
    const { container } = render(<OutputViewport />)
    expect(container.firstChild).not.toBeNull()
  })

  it('creates and opens an xterm Terminal instance', () => {
    render(<OutputViewport />)
    expect(mocks.MockTerminal).toHaveBeenCalledOnce()
    expect(mocks.mockOpen).toHaveBeenCalledOnce()
  })

  it('loads and fits the FitAddon', () => {
    render(<OutputViewport />)
    expect(mocks.MockFitAddon).toHaveBeenCalledOnce()
    expect(mocks.mockLoadAddon).toHaveBeenCalledOnce()
    expect(mocks.mockFit).toHaveBeenCalledOnce()
  })

  it('disposes the terminal on unmount', () => {
    const { unmount } = render(<OutputViewport />)
    unmount()
    expect(mocks.mockDispose).toHaveBeenCalledOnce()
  })
})
