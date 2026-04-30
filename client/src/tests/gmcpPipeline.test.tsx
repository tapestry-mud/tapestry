import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { ProtocolParser } from '../connection/ProtocolParser'
import { initCoreHandlers, GmcpDispatcher } from '../connection/GmcpDispatcher'
import { VitalsBar } from '../panels/VitalsBar'
import { useCharStore } from '../stores/charStore'
import { useRoomStore } from '../stores/roomStore'
import { useChatStore } from '../stores/chatStore'
import { useOutputStore } from '../stores/outputStore'
import { useDebugStore } from '../stores/debugStore'
import { getTerminal } from '../terminal/terminalStore'

vi.mock('../terminal/terminalStore', () => ({
  getTerminal: vi.fn(),
  setTerminal: vi.fn(),
}))

const mockWrite = vi.fn()

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(getTerminal).mockReturnValue({ write: mockWrite } as never)
  GmcpDispatcher.clear()
  initCoreHandlers()
  useCharStore.setState({ name: '', race: '', class: '', level: 0, hp: 0, maxHp: 0, mana: 0, maxMana: 0, mv: 0, maxMv: 0 })
  useRoomStore.setState({ current: { num: 0, name: '', area: '', environment: '', exits: {} }, mapGraph: new Map(), lastDirection: null })
  useChatStore.setState({ messages: [], activeFilter: 'all', unreadCount: 0 })
  useOutputStore.setState({ lines: [], scrollLocked: false })
  useDebugStore.setState({ gmcpLog: [], textLog: [], commandLog: [], connectionLog: [], isOpen: false, activeTab: 'gmcp' })
})

describe('GMCP pipeline integration', () => {
  it('Char.Vitals envelope updates charStore and VitalsBar renders HP', () => {
    ProtocolParser.parseMessage(JSON.stringify({
      type: 'gmcp',
      package: 'Char.Vitals',
      data: { hp: 340, maxhp: 340, mana: 120, maxmana: 150, mv: 200, maxmv: 200 },
    }))

    useCharStore.setState((s) => ({ ...s, name: 'Raegar', class: 'Warrior', level: 10 }))
    useRoomStore.setState((s) => ({ ...s, current: { ...s.current, name: 'Town Square' } }))

    render(<VitalsBar />)
    expect(screen.getByText(/340/)).toBeDefined()
  })

  it('Char.Status envelope sets character name', () => {
    ProtocolParser.parseMessage(JSON.stringify({
      type: 'gmcp',
      package: 'Char.Status',
      data: { name: 'Raegar', race: 'Human', class: 'Warrior', level: 10 },
    }))
    expect(useCharStore.getState().name).toBe('Raegar')
  })

  it('Room.Info envelope updates roomStore', () => {
    ProtocolParser.parseMessage(JSON.stringify({
      type: 'gmcp',
      package: 'Room.Info',
      data: { num: 'core:town-square', name: 'Town Square', area: 'Midgaard', environment: 'city', exits: { north: 'core:inn' } },
    }))
    expect(useRoomStore.getState().current.name).toBe('Town Square')
    expect(useRoomStore.getState().mapGraph.has('core:town-square')).toBe(true)
  })

  it('Room.WrongDir envelope removes last direction from roomStore', () => {
    useRoomStore.setState((s) => ({ ...s, lastDirection: 'north', current: { ...s.current, exits: { north: 2 } } }))
    ProtocolParser.parseMessage(JSON.stringify({
      type: 'gmcp',
      package: 'Room.WrongDir',
      data: {},
    }))
    expect(useRoomStore.getState().current.exits['north']).toBeUndefined()
    expect(mockWrite).not.toHaveBeenCalledWith("You can't go that way.\r\n")
  })

  it('Comm.Channel envelope adds message to chatStore', () => {
    ProtocolParser.parseMessage(JSON.stringify({
      type: 'gmcp',
      package: 'Comm.Channel',
      data: { channel: 'chat', sender: 'Raegar', text: 'Hello!' },
    }))
    expect(useChatStore.getState().messages).toHaveLength(1)
    expect(useChatStore.getState().messages[0].sender).toBe('Raegar')
  })

  it('malformed Char.Vitals (wrong types) does not crash or corrupt store', () => {
    const before = useCharStore.getState().hp
    ProtocolParser.parseMessage(JSON.stringify({
      type: 'gmcp',
      package: 'Char.Vitals',
      data: { hp: 'not-a-number', maxhp: 340, mana: 120, maxmana: 150, mv: 200, maxmv: 200 },
    }))
    expect(useCharStore.getState().hp).toBe(before)
  })

  it('unknown GMCP package is logged in debugStore and does not throw', () => {
    expect(() => {
      ProtocolParser.parseMessage(JSON.stringify({
        type: 'gmcp',
        package: 'Unknown.Package',
        data: { something: true },
      }))
    }).not.toThrow()
    expect(useDebugStore.getState().gmcpLog.some((e) => e.package === 'Unknown.Package')).toBe(true)
  })
})
