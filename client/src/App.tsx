import { useEffect } from 'react'
import { ConnectScreen } from './layout/ConnectScreen'
import { GameLayout } from './layout/GameLayout'
import { LoginLayout } from './layout/LoginLayout'
import { useConnectionStore } from './stores/connectionStore'
import { useDebugStore } from './stores/debugStore'
import { useAffectsStore } from './stores/affectsStore'
import { initCoreHandlers } from './connection/GmcpDispatcher'
import { useShortcutStore } from './stores/shortcutStore'
import { registerAllShortcuts } from './accessibility/shortcuts/registerAll'
import { HelpModal } from './panels/HelpModal'

initCoreHandlers()
registerAllShortcuts()

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
        return
      }
      // startsWith('Key') limits dispatch to letter keys only; Alt+1, Alt+F1, etc. will not trigger shortcuts
      if (e.altKey && e.code.startsWith('Key')) {
        const passthrough = (document.activeElement as HTMLElement | null)?.dataset?.shortcutPassthrough
        if (passthrough === 'false') { return }
        const letter = e.code.slice(3)
        const keyStr = `Alt+${letter}`
        const entry = useShortcutStore.getState().getByKey(keyStr)
        if (entry && entry.enabled) {
          e.preventDefault()
          entry.handler()
        }
      }
    }
    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [toggleDebug])

  if (status === 'disconnected' || status === 'error') {
    return <ConnectScreen />
  }
  if (loginPhase === 'playing') {
    return (
      <>
        {/* Mounted at App level (not inside GameLayout) so it's available during chargen */}
        <HelpModal />
        <GameLayout />
      </>
    )
  }
  return (
    <>
      {/* Mounted at App level (not inside GameLayout) so it's available during chargen */}
      <HelpModal />
      <LoginLayout />
    </>
  )
}
