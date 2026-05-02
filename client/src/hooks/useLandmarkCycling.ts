import { useEffect } from 'react'

const LANDMARK_IDS = ['command-input', 'left-panels', 'right-panels']

export function useLandmarkCycling() {
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.key !== 'F6') { return }
      e.preventDefault()

      const activeId = document.activeElement?.closest('[id]')?.id ?? ''
      const currentIndex = LANDMARK_IDS.indexOf(activeId)
      const nextIndex = (currentIndex + 1) % LANDMARK_IDS.length
      const target = document.getElementById(LANDMARK_IDS[nextIndex])

      if (target) {
        const focusable = target.querySelector<HTMLElement>('input, button, [tabindex="0"]')
        if (focusable) {
          focusable.focus()
        } else {
          target.focus()
        }
      }
    }

    window.addEventListener('keydown', handleKeyDown)
    return () => { window.removeEventListener('keydown', handleKeyDown) }
  }, [])
}
