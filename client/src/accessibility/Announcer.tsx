import { useAnnounceStore } from './announceStore'

export function Announcer() {
  const assertiveMessage = useAnnounceStore((s) => s.assertiveMessage)
  const politeMessage = useAnnounceStore((s) => s.politeMessage)

  return (
    <>
      <div
        role="alert"
        aria-live="assertive"
        aria-atomic="true"
        className="sr-only"
      >
        {assertiveMessage}
      </div>
      <div
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
      >
        {politeMessage}
      </div>
    </>
  )
}
