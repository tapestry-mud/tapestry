import { useConnectionStore } from '../stores/connectionStore'
import { useDebugStore } from '../stores/debugStore'
import { useRoomStore } from '../stores/roomStore'
import { ProtocolParser } from './ProtocolParser'
import { getTerminal } from '../terminal/terminalStore'

const BACKOFF_DELAYS = [1000, 2000, 4000, 8000, 16000, 30000]

let ws: WebSocket | null = null
let reconnectAttempt = 0
let reconnectTimer: ReturnType<typeof setTimeout> | null = null
let shouldReconnect = false

function deriveServerUrl(): string | null {
  const { hostname, host, protocol } = window.location
  if (hostname === 'localhost' || hostname === '127.0.0.1') {
    return null
  }
  const wsProtocol = protocol === 'https:' ? 'wss:' : 'ws:'
  return `${wsProtocol}//${host}/ws`
}

function connect(address: string): void {
  const url = address.startsWith('ws') ? address : `ws://${address}`
  useConnectionStore.getState().setServerAddress(address)
  useConnectionStore.getState().setStatus('connecting')
  useDebugStore.getState().logConnection('connecting', url)

  ws = new WebSocket(url)

  ws.onopen = () => {
    reconnectAttempt = 0
    shouldReconnect = true
    useConnectionStore.getState().setStatus('connected')
    useConnectionStore.getState().setError(null)
    useDebugStore.getState().logConnection('connected', url)
    useConnectionStore.getState().setLoginPhase('name')
    sendGmcp('Core.Supports.Set', ['Char 1', 'Room 1', 'Comm 1', 'Login 1', 'Response 1'])
  }

  ws.onmessage = (event) => {
    ProtocolParser.parseMessage(event.data as string)
  }

  ws.onerror = () => {
    useConnectionStore.getState().setStatus('error')
    useConnectionStore.getState().setError('WebSocket error')
    useDebugStore.getState().logConnection('error', url)
  }

  ws.onclose = () => {
    useDebugStore.getState().logConnection('disconnected', url)
    if (shouldReconnect) {
      const delay = BACKOFF_DELAYS[Math.min(reconnectAttempt, BACKOFF_DELAYS.length - 1)]
      reconnectAttempt++
      useConnectionStore.getState().setStatus('connecting')
      useConnectionStore.getState().setLoginPhase('name')
      reconnectTimer = setTimeout(() => { connect(address) }, delay)
    } else {
      useConnectionStore.getState().setStatus('disconnected')
      useConnectionStore.getState().setLoginPhase('disconnected')
    }
  }
}

function disconnect(): void {
  shouldReconnect = false
  if (reconnectTimer) {
    clearTimeout(reconnectTimer)
    reconnectTimer = null
  }
  ws?.close()
  ws = null
  useConnectionStore.getState().setStatus('disconnected')
  useConnectionStore.getState().setLoginPhase('disconnected')
  useDebugStore.getState().logConnection('disconnected', 'user disconnect')
}

function send(command: string): void {
  if (ws?.readyState !== WebSocket.OPEN) { return }
  const payload = JSON.stringify({ type: 'command', data: command })
  ws.send(payload)

  const { loginPhase } = useConnectionStore.getState()
  if (loginPhase !== 'password') {
    getTerminal()?.write(command + '\r\n')
    useRoomStore.getState().setLastDirection(command)
  }
  useDebugStore.getState().logCommand(loginPhase === 'password' ? '[password]' : command)
}

function sendGmcp(pkg: string, data: unknown): void {
  if (ws?.readyState !== WebSocket.OPEN) { return }
  const payload = JSON.stringify({ type: 'gmcp', package: pkg, data })
  ws.send(payload)
  useDebugStore.getState().logGmcp(pkg, data, 'out')
}

export const WebSocketClient = { connect, disconnect, send, sendGmcp, deriveServerUrl }
