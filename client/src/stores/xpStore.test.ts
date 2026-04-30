import { beforeEach, describe, it, expect } from 'vitest'
import { useXpStore } from './xpStore'

beforeEach(() => { useXpStore.setState({ tracks: [] }) })

describe('xpStore', () => {
  it('starts with no tracks', () => {
    expect(useXpStore.getState().tracks).toHaveLength(0)
  })

  it('setTracks replaces track list', () => {
    useXpStore.getState().setTracks({
      tracks: [
        { name: 'core', level: 5, xp: 450, xpToNext: 550, currentLevelThreshold: 0 },
      ],
    })
    const tracks = useXpStore.getState().tracks
    expect(tracks).toHaveLength(1)
    expect(tracks[0].name).toBe('core')
    expect(tracks[0].level).toBe(5)
    expect(tracks[0].xp).toBe(450)
    expect(tracks[0].xpToNext).toBe(550)
  })

  it('setTracks with multiple tracks', () => {
    useXpStore.getState().setTracks({
      tracks: [
        { name: 'core', level: 5, xp: 450, xpToNext: 550, currentLevelThreshold: 0 },
        { name: 'combat', level: 3, xp: 300, xpToNext: 700, currentLevelThreshold: 200 },
      ],
    })
    expect(useXpStore.getState().tracks).toHaveLength(2)
  })

  it('setTracks overwrites previous tracks', () => {
    useXpStore.getState().setTracks({
      tracks: [{ name: 'core', level: 1, xp: 0, xpToNext: 1000, currentLevelThreshold: 0 }],
    })
    useXpStore.getState().setTracks({
      tracks: [{ name: 'core', level: 2, xp: 1100, xpToNext: 900, currentLevelThreshold: 1000 }],
    })
    const s = useXpStore.getState()
    expect(s.tracks[0].level).toBe(2)
    expect(s.tracks[0].xp).toBe(1100)
  })
})
