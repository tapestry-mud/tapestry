import '@xterm/xterm/css/xterm.css'
import { useRef, useEffect, useState } from 'react'
import { Terminal } from '@xterm/xterm'
import { FitAddon } from '@xterm/addon-fit'
import { setTerminal } from '../terminal/terminalStore'

export function OutputViewport() {
  const containerRef = useRef<HTMLDivElement>(null)
  const terminalRef = useRef<Terminal | null>(null)
  const [scrolledUp, setScrolledUp] = useState(false)

  useEffect(() => {
    if (!containerRef.current) { return }

    const terminal = new Terminal({
      cursorBlink: false,
      cursorStyle: 'bar',
      disableStdin: true,
      scrollback: 5000,
      fontFamily: "'JetBrains Mono', 'Fira Code', 'Cascadia Code', monospace",
      fontSize: 14,
      theme: {
        background: '#0d0d1a',
        foreground: '#e0e0e0',
        cursor: '#5b8a9a',
        selectionBackground: '#5b8a9a44',
        black: '#000000',
        red: '#aa0000',
        green: '#00aa00',
        yellow: '#aa5500',
        blue: '#0000aa',
        magenta: '#aa00aa',
        cyan: '#00aaaa',
        white: '#aaaaaa',
        brightBlack: '#555555',
        brightRed: '#ff5555',
        brightGreen: '#55ff55',
        brightYellow: '#ffff55',
        brightBlue: '#5555ff',
        brightMagenta: '#ff55ff',
        brightCyan: '#55ffff',
        brightWhite: '#ffffff',
      },
    })

    const fitAddon = new FitAddon()
    terminal.loadAddon(fitAddon)
    terminal.open(containerRef.current)
    fitAddon.fit()
    setTerminal(terminal)
    terminalRef.current = terminal

    terminal.onScroll(() => {
      const buf = terminal.buffer.active
      const atBottom = buf.viewportY + terminal.rows >= buf.length
      setScrolledUp(!atBottom)
    })

    const observer = new ResizeObserver(() => { fitAddon.fit() })
    observer.observe(containerRef.current)

    return () => {
      observer.disconnect()
      setTerminal(null)
      terminalRef.current = null
      terminal.dispose()
    }
  }, [])

  function scrollToBottom() {
    terminalRef.current?.scrollToBottom()
    setScrolledUp(false)
  }

  return (
    <div className="relative flex-1 overflow-hidden bg-surface-deep pl-3">
      <div ref={containerRef} className="h-full w-full" />
      {scrolledUp && (
        <button
          onClick={scrollToBottom}
          title="Scroll to bottom"
          className="absolute bottom-2 right-3 bg-accent text-white text-xs px-2 py-1 rounded hover:opacity-80"
        >
          v bottom
        </button>
      )}
    </div>
  )
}
