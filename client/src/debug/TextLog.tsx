import { useDebugStore } from '../stores/debugStore'

export function TextLog() {
  const { textLog } = useDebugStore()

  return (
    <div className="h-full overflow-y-auto p-2 font-mono text-xs flex flex-col gap-0.5">
      {textLog.length === 0 ? (
        <span className="text-text-secondary italic">No text received</span>
      ) : (
        [...textLog].reverse().map((entry, i) => (
          <div key={i} className="flex gap-2">
            <span className="text-text-secondary shrink-0">{new Date(entry.timestamp).toISOString().slice(11, 23)}</span>
            <span className="text-text-primary whitespace-pre break-all">{JSON.stringify(entry.raw)}</span>
          </div>
        ))
      )}
    </div>
  )
}
