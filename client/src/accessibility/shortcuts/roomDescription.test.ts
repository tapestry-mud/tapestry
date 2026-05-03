import { beforeEach, describe, it, expect, vi } from 'vitest'
import { handleRoomDescription, stripMarkup } from './roomDescription'
import { useRoomStore } from '../../stores/roomStore'
import { useAnnounceStore } from '../announceStore'

const nullRoom = {
  num: '', name: '', area: '', environment: '', description: '',
  weatherExposed: false, timeExposed: false, doors: {}, exits: {},
}

beforeEach(() => {
  useRoomStore.setState({ current: { ...nullRoom }, mapGraph: new Map(), lastDirection: null })
})

describe('handleRoomDescription', () => {
  it('calls pushMessage assertive with name, description, and exits', () => {
    useRoomStore.setState({
      current: {
        ...nullRoom,
        name: 'Town Square',
        description: 'A busy square.',
        exits: { north: 'core:inn', south: 'core:gate' },
      },
    })
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleRoomDescription()
    expect(pushSpy).toHaveBeenCalledWith(
      'Town Square. A busy square. Exits: north, south.',
      'assertive'
    )
    pushSpy.mockRestore()
  })

  it('says No exits when room has none', () => {
    useRoomStore.setState({
      current: { ...nullRoom, name: 'Dead End', description: 'A dead end.', exits: {} },
    })
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleRoomDescription()
    expect(pushSpy).toHaveBeenCalledWith('Dead End. A dead end. No exits.', 'assertive')
    pushSpy.mockRestore()
  })

  it('handles description without trailing period', () => {
    useRoomStore.setState({
      current: { ...nullRoom, name: 'Hallway', description: 'A narrow hallway', exits: {} },
    })
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleRoomDescription()
    expect(pushSpy).toHaveBeenCalledWith('Hallway. A narrow hallway. No exits.', 'assertive')
    pushSpy.mockRestore()
  })

  it('strips ANSI escape codes from description before announcing', () => {
    useRoomStore.setState({
      current: {
        ...nullRoom,
        name: 'Dungeon',
        description: '\x1b[1;32mA damp dungeon.\x1b[0m',
        exits: {},
      },
    })
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleRoomDescription()
    expect(pushSpy).toHaveBeenCalledWith('Dungeon. A damp dungeon. No exits.', 'assertive')
    pushSpy.mockRestore()
  })

  it('strips Tapestry color tags from description before announcing', () => {
    useRoomStore.setState({
      current: {
        ...nullRoom,
        name: 'Forest',
        description: '{cyan}A dense forest.{reset}',
        exits: { east: 'core:road' },
      },
    })
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleRoomDescription()
    expect(pushSpy).toHaveBeenCalledWith('Forest. A dense forest. Exits: east.', 'assertive')
    pushSpy.mockRestore()
  })

  it('strips markup from name before announcing', () => {
    useRoomStore.setState({
      current: {
        ...nullRoom,
        name: '{bold}Town Square{reset}',
        description: 'A busy square.',
        exits: {},
      },
    })
    const pushSpy = vi.spyOn(useAnnounceStore.getState(), 'pushMessage')
    handleRoomDescription()
    expect(pushSpy).toHaveBeenCalledWith('Town Square. A busy square. No exits.', 'assertive')
    pushSpy.mockRestore()
  })
})

describe('stripMarkup', () => {
  it('removes ANSI escape sequences', () => {
    expect(stripMarkup('\x1b[1;32mHello\x1b[0m')).toBe('Hello')
  })

  it('removes bare bracket-form ANSI sequences', () => {
    expect(stripMarkup('[1mBold[0m')).toBe('Bold')
  })

  it('removes Tapestry color/style tags', () => {
    expect(stripMarkup('{cyan}Hello{reset}')).toBe('Hello')
  })

  it('removes HTML-like tags', () => {
    expect(stripMarkup('<b>Hello</b>')).toBe('Hello')
  })

  it('returns plain text unchanged', () => {
    expect(stripMarkup('Just plain text.')).toBe('Just plain text.')
  })
})
