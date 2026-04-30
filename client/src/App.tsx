import { useEffect } from 'react'
import { ConnectScreen } from './layout/ConnectScreen'
import { GameLayout } from './layout/GameLayout'
import { LoginLayout } from './layout/LoginLayout'
import { useConnectionStore } from './stores/connectionStore'
import { useDebugStore } from './stores/debugStore'
import { useAffectsStore } from './stores/affectsStore'
import { initCoreHandlers } from './connection/GmcpDispatcher'

initCoreHandlers()

export default function App() {
  const status = useConnectionStore((s) => s.status)
  const loginPhase = useConnectionStore((s) => s.loginPhase)
  const toggleDebug = useDebugStore((s) => s.toggleOpen)

  useEffect(() => {
    const id = setInterval(() => {
      useAffectsStore.getState().tickTimers()
    }, 2000)
    return () => clearInterval(id)
  }, [])

  useEffect(() => {
    function handleKey(e: KeyboardEvent) {
      if (e.key === '`' && !e.ctrlKey && !e.metaKey) {
        e.preventDefault()
        toggleDebug()
      }
    }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [toggleDebug])

  if (status === 'disconnected' || status === 'error') {
    return <ConnectScreen />
  }
  if (loginPhase === 'playing') {
    return <GameLayout />
  }
  return <LoginLayout />
}
