import { beforeEach, describe, it, expect, vi } from 'vitest'
import { handleRoomDescription } from './roomDescription'
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
})
