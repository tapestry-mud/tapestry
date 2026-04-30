import { beforeEach, describe, it, expect } from 'vitest'
import { useDebugStore } from './debugStore'

beforeEach(() => {
  useDebugStore.setState({ gmcpLog: [], textLog: [], commandLog: [], connectionLog: [], isOpen: false, activeTab: 'gmcp' })
})

describe('debugStore', () => {
  it('logGmcp appends entry with direction and timestamp', () => {
    useDebugStore.getState().logGmcp('Char.Vitals', { hp: 100 }, 'in')
    const entry = useDebugStore.getState().gmcpLog[0]
    expect(entry.package).toBe('Char.Vitals')
    expect(entry.direction).toBe('in')
    expect(entry.timestamp).toBeGreaterThan(0)
  })

  it('logText appends raw text entry', () => {
    useDebugStore.getState().logText('raw text')
    expect(useDebugStore.getState().textLog[0].raw).toBe('raw text')
  })

  it('logCommand appends command entry', () => {
    useDebugStore.getState().logCommand('look')
    expect(useDebugStore.getState().commandLog[0].command).toBe('look')
  })

  it('logConnection appends event entry', () => {
    useDebugStore.getState().logConnection('connected', 'ws://localhost:4001')
    const entry = useDebugStore.getState().connectionLog[0]
    expect(entry.event).toBe('connected')
    expect(entry.detail).toBe('ws://localhost:4001')
  })

  it('toggleOpen flips isOpen', () => {
    useDebugStore.getState().toggleOpen()
    expect(useDebugStore.getState().isOpen).toBe(true)
    useDebugStore.getState().toggleOpen()
    expect(useDebugStore.getState().isOpen).toBe(false)
  })

  it('setTab updates activeTab', () => {
    useDebugStore.getState().setTab('state')
    expect(useDebugStore.getState().activeTab).toBe('state')
  })

  it('gmcpLog caps at 500 entries', () => {
    const store = useDebugStore.getState()
    for (let i = 0; i < 505; i++) { store.logGmcp('Char.Vitals', {}, 'in') }
    expect(useDebugStore.getState().gmcpLog).toHaveLength(500)
  })
})
