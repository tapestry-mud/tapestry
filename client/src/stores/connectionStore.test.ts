import { beforeEach, describe, it, expect } from 'vitest'
import { useConnectionStore } from './connectionStore'
import type { LoginPhaseState } from '../types/gmcp'

beforeEach(() => {
  useConnectionStore.setState({ status: 'disconnected', serverAddress: '', error: null })
})

describe('connectionStore', () => {
  it('starts disconnected', () => {
    expect(useConnectionStore.getState().status).toBe('disconnected')
  })

  it('setStatus updates status', () => {
    useConnectionStore.getState().setStatus('connected')
    expect(useConnectionStore.getState().status).toBe('connected')
  })

  it('setError sets error message', () => {
    useConnectionStore.getState().setError('connection refused')
    expect(useConnectionStore.getState().error).toBe('connection refused')
  })

  it('setServerAddress updates address', () => {
    useConnectionStore.getState().setServerAddress('localhost:4001')
    expect(useConnectionStore.getState().serverAddress).toBe('localhost:4001')
  })
})

describe('connectionStore loginPhase', () => {
  beforeEach(() => {
    useConnectionStore.setState({
      status: 'disconnected',
      serverAddress: '',
      error: null,
      loginPhase: 'disconnected',
    })
  })

  it('starts with loginPhase disconnected', () => {
    expect(useConnectionStore.getState().loginPhase).toBe('disconnected')
  })

  it('setLoginPhase updates loginPhase', () => {
    useConnectionStore.getState().setLoginPhase('password')
    expect(useConnectionStore.getState().loginPhase).toBe('password')
  })

  it('setLoginPhase to playing works', () => {
    useConnectionStore.getState().setLoginPhase('playing')
    expect(useConnectionStore.getState().loginPhase).toBe('playing')
  })
})
