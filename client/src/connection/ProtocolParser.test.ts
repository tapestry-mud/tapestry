import { describe, it, expect, beforeEach, vi } from 'vitest'
import { ProtocolParser } from './ProtocolParser'
import { useDebugStore } from '../stores/debugStore'
import { getTerminal } from '../terminal/terminalStore'

vi.mock('../terminal/terminalStore', () => ({
  getTerminal: vi.fn(),
  setTerminal: vi.fn(),
}))

beforeEach(() => {
  vi.clearAllMocks()
  useDebugStore.setState({ gmcpLog: [], textLog: [], commandLog: [], connectionLog: [], isOpen: false, activeTab: 'gmcp' })
})

describe('ProtocolParser', () => {
  it('writes text envelope directly to the terminal', () => {
    const mockWrite = vi.fn()
    vi.mocked(getTerminal).mockReturnValue({ write: mockWrite } as never)
    ProtocolParser.parseMessage(JSON.stringify({ type: 'text', data: 'A goblin is here.\r\n' }))
    expect(mockWrite).toHaveBeenCalledWith('A goblin is here.\r\n')
  })

  it('tees text to debugStore textLog', () => {
    vi.mocked(getTerminal).mockReturnValue({ write: vi.fn() } as never)
    ProtocolParser.parseMessage(JSON.stringify({ type: 'text', data: 'test\r\n' }))
    expect(useDebugStore.getState().textLog[0].raw).toBe('test\r\n')
  })

  it('routes gmcp envelope to debugStore gmcpLog', () => {
    ProtocolParser.parseMessage(JSON.stringify({ type: 'gmcp', package: 'Test.Pkg', data: { v: 1 } }))
    expect(useDebugStore.getState().gmcpLog[0].package).toBe('Test.Pkg')
  })

  it('does not throw when terminal is not yet mounted', () => {
    vi.mocked(getTerminal).mockReturnValue(null)
    expect(() => ProtocolParser.parseMessage(JSON.stringify({ type: 'text', data: 'hello\r\n' }))).not.toThrow()
  })

  it('silently ignores malformed JSON', () => {
    expect(() => ProtocolParser.parseMessage('not json')).not.toThrow()
  })

  it('silently ignores unknown envelope type', () => {
    expect(() => ProtocolParser.parseMessage(JSON.stringify({ type: 'ping' }))).not.toThrow()
  })
})
