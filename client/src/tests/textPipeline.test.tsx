import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render } from '@testing-library/react'
import { ProtocolParser } from '../connection/ProtocolParser'
import { OutputViewport } from '../panels/OutputViewport'
import { useDebugStore } from '../stores/debugStore'
import { getTerminal } from '../terminal/terminalStore'

const mocks = vi.hoisted(() => {
  const mockOpen = vi.fn()
  const mockLoadAddon = vi.fn()
  const mockDispose = vi.fn()
  const mockFit = vi.fn()
  // Must use function (not arrow) so the mocks can be called with `new`
  const MockTerminal = vi.fn(function(this: Record<string, unknown>) {
    this.open = mockOpen
    this.loadAddon = mockLoadAddon
    this.dispose = mockDispose
    this.write = vi.fn()
    this.onScroll = vi.fn()
    this.buffer = { active: { viewportY: 0, length: 0 } }
    this.rows = 24
  })
  const MockFitAddon = vi.fn(function(this: Record<string, unknown>) {
    this.fit = mockFit
  })
  return { MockTerminal, MockFitAddon, mockOpen, mockLoadAddon, mockDispose, mockFit }
})

vi.mock('../terminal/terminalStore', () => ({
  getTerminal: vi.fn(),
  setTerminal: vi.fn(),
}))
vi.mock('@xterm/xterm', () => ({ Terminal: mocks.MockTerminal }))
vi.mock('@xterm/addon-fit', () => ({ FitAddon: mocks.MockFitAddon }))

beforeEach(() => {
  vi.clearAllMocks()
  useDebugStore.setState({ gmcpLog: [], textLog: [], commandLog: [], connectionLog: [], isOpen: false, activeTab: 'gmcp' })
})

describe('Text pipeline integration', () => {
  it('text envelope is written as-is to xterm (ANSI codes preserved)', () => {
    const mockWrite = vi.fn()
    vi.mocked(getTerminal).mockReturnValue({ write: mockWrite } as never)
    const raw = '\x1b[31mA goblin\x1b[0m shaman is here.\r\n'
    ProtocolParser.parseMessage(JSON.stringify({ type: 'text', data: raw }))
    expect(mockWrite).toHaveBeenCalledWith(raw)
  })

  it('text is teed to debugStore textLog', () => {
    vi.mocked(getTerminal).mockReturnValue({ write: vi.fn() } as never)
    ProtocolParser.parseMessage(JSON.stringify({ type: 'text', data: 'test\r\n' }))
    expect(useDebugStore.getState().textLog).toHaveLength(1)
  })

  it('OutputViewport mounts xterm terminal', () => {
    render(<OutputViewport />)
    expect(mocks.MockTerminal).toHaveBeenCalledOnce()
    expect(mocks.mockOpen).toHaveBeenCalledOnce()
  })

  it('server text is passed to xterm, not injected as HTML', () => {
    const mockWrite = vi.fn()
    vi.mocked(getTerminal).mockReturnValue({ write: mockWrite } as never)
    const xss = '<img src=x onerror=alert(1)>\r\n'
    ProtocolParser.parseMessage(JSON.stringify({ type: 'text', data: xss }))
    render(<OutputViewport />)
    expect(document.querySelector('img')).toBeNull()
    expect(mockWrite).toHaveBeenCalledWith(xss)
  })
})
