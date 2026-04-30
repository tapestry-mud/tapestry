import { beforeEach, describe, it, expect } from 'vitest'
import { useWorldStore } from './worldStore'

beforeEach(() => {
  useWorldStore.setState({ hour: 0, period: null, dayCount: 0, weatherState: null })
})

describe('worldStore', () => {
  it('setTime updates all time fields', () => {
    useWorldStore.getState().setTime({ hour: 14, period: 'day', dayCount: 42 })
    const s = useWorldStore.getState()
    expect(s.hour).toBe(14)
    expect(s.period).toBe('day')
    expect(s.dayCount).toBe(42)
  })

  it('setWeather updates weatherState', () => {
    useWorldStore.getState().setWeather('storm')
    expect(useWorldStore.getState().weatherState).toBe('storm')
  })
})
