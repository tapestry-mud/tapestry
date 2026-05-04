import { useEffect, useRef } from 'react'
import { useHelpStore, type HelpTopicData, type HelpTopicSummary } from '../stores/helpStore'
import { parseColorTags } from '../utils/text'
import { WebSocketClient } from '../connection/WebSocketClient'

let savedFocus: Element | null = null

export function HelpModal() {
    const { isOpen, response, closeHelp, inDialogKey, inDialogAnnouncement } = useHelpStore()
    const dialogRef = useRef<HTMLDialogElement>(null)
    const headingRef = useRef<HTMLHeadingElement>(null)

    useEffect(() => {
        const dialog = dialogRef.current
        if (!dialog) { return }
        if (isOpen) {
            if (!savedFocus) { savedFocus = document.activeElement }
            dialog.showModal()
        } else {
            dialog.close()
            if (savedFocus instanceof HTMLElement) { savedFocus.focus() }
            savedFocus = null
        }
    }, [isOpen])

    // Prevent native <dialog> Escape from closing without updating React state
    useEffect(() => {
        const dialog = dialogRef.current
        if (!dialog) { return }
        const handleCancel = (e: Event) => { e.preventDefault(); closeHelp() }
        dialog.addEventListener('cancel', handleCancel)
        return () => { dialog.removeEventListener('cancel', handleCancel) }
    }, [closeHelp])

    // Move focus to heading when response changes so focus doesn't fall to dialog root
    useEffect(() => {
        if (!isOpen || !response) { return }
        headingRef.current?.focus()
    }, [isOpen, response])

    return (
        <dialog
            ref={dialogRef}
            aria-labelledby="help-modal-title"
            className="fixed z-50 m-auto max-w-2xl w-full max-h-[80vh] rounded-lg bg-gray-900 text-gray-100 shadow-2xl p-0 backdrop:bg-black/60 border border-gray-700"
            onClick={(e) => { if (e.target === dialogRef.current) { closeHelp() } }}
        >
            {response && (
                <div className="relative flex flex-col max-h-[80vh]">
                    <div className="px-4 py-3 border-b border-gray-700 shrink-0 pr-12">
                        <h2 id="help-modal-title" ref={headingRef} tabIndex={-1} className="text-lg font-semibold outline-none">
                            {response.status === 'ok'
                                ? response.topic.title
                                : response.status === 'multiple'
                                    ? (response.term ? `Help: "${response.term}"` : 'Help Topics')
                                    : response.status === 'no_match'
                                        ? `Help: "${response.term}"`
                                        : ''}
                        </h2>
                    </div>
                    {inDialogAnnouncement && (
                        <div key={inDialogKey} role="alert" aria-live="assertive" aria-atomic="true" className="sr-only">
                            {inDialogAnnouncement}
                        </div>
                    )}
                    <div className="overflow-y-auto px-4 py-4 flex-1">
                        {response.status === 'ok' && <HelpTopicContent topic={response.topic} />}
                        {response.status === 'multiple' && (
                            <HelpDisambiguation term={response.term} matches={response.matches} />
                        )}
                        {response.status === 'no_match' && (
                            <p className="text-gray-400 text-sm">No help topic found for "{response.term}".</p>
                        )}
                    </div>
                    <div className="sr-only">
                        {response.status === 'ok'
                            ? 'Press Alt+H to read detailed help text. Press Escape to exit.'
                            : 'Press Escape to exit.'}
                    </div>
                    <button
                        onClick={closeHelp}
                        className="absolute top-3 right-4 text-gray-400 hover:text-gray-100 text-xl leading-none"
                        aria-label="Close help"
                    >
                        &times;
                    </button>
                </div>
            )}
        </dialog>
    )
}

function sendCommand(cmd: string) {
    WebSocketClient.send(cmd)
}

function HelpTopicContent({ topic }: { topic: HelpTopicData }) {
    return (
        <div className="space-y-4">
            <p className="text-gray-400 italic">{topic.brief}</p>

            {topic.syntax.length > 0 && (
                <div>
                    <p className="text-sm font-semibold text-gray-300 mb-1">Syntax:</p>
                    <div className="bg-gray-800 rounded px-3 py-2 font-mono text-sm space-y-1">
                        {topic.syntax.map((s, i) => <div key={i}>{s}</div>)}
                    </div>
                </div>
            )}

            <div aria-hidden="true" className="text-sm leading-relaxed whitespace-pre-wrap">
                <ColorTaggedText text={topic.body} />
            </div>

            {topic.seeAlso.length > 0 && (
                <div className="pt-2 border-t border-gray-700 text-sm">
                    <span className="text-gray-400">See also: </span>
                    {topic.seeAlso.map((id, i) => (
                        <span key={id}>
                            <button
                                className="text-blue-400 hover:underline"
                                onClick={() => { sendCommand(`help ${id}`) }}
                            >
                                {id}
                            </button>
                            {i < topic.seeAlso.length - 1 && <span className="text-gray-500">, </span>}
                        </span>
                    ))}
                </div>
            )}
        </div>
    )
}

function HelpDisambiguation({ term, matches }: { term: string; matches: HelpTopicSummary[] }) {
    return (
        <div className="space-y-2">
            <p className="text-gray-400 text-sm">
                {term ? `Multiple topics match "${term}":` : 'Select a category:'}
            </p>
            <ul className="space-y-1">
                {matches.map((m) => (
                    <li key={m.id}>
                        <button
                            className="text-left w-full hover:bg-gray-800 rounded px-2 py-1.5 flex gap-3"
                            onClick={() => { sendCommand(`help ${m.id}`) }}
                        >
                            {m.id !== m.title.toLowerCase()
                                ? <><span className="text-blue-400 font-mono shrink-0">{m.id}</span><span className="text-gray-300">{m.title}</span></>
                                : <span className="text-blue-400 font-mono">{m.title}</span>
                            }
                        </button>
                    </li>
                ))}
            </ul>
        </div>
    )
}

function ColorTaggedText({ text }: { text: string }) {
    const parts = parseColorTags(text)
    return (
        <>
            {parts.map((part, i) =>
                part.className
                    ? <span key={i} className={part.className}>{part.text}</span>
                    : <span key={i}>{part.text}</span>
            )}
        </>
    )
}
