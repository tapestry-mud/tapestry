import { useEffect, useRef } from 'react'
import { useHelpStore, type HelpTopicData, type HelpTopicSummary } from '../stores/helpStore'
import { parseColorTags, stripMarkup } from '../utils/text'
import { useAnnounceStore } from '../accessibility/announceStore'
import { WebSocketClient } from '../connection/WebSocketClient'

export function HelpModal() {
    const { isOpen, response, closeHelp } = useHelpStore()
    const dialogRef = useRef<HTMLDialogElement>(null)
    const previousFocusRef = useRef<Element | null>(null)

    useEffect(() => {
        const dialog = dialogRef.current
        if (!dialog) { return }
        if (isOpen) {
            previousFocusRef.current = document.activeElement
            dialog.showModal()
        } else {
            dialog.close()
            if (previousFocusRef.current instanceof HTMLElement) {
                previousFocusRef.current.focus()
            }
        }
    }, [isOpen])

    useEffect(() => {
        if (!isOpen || !response) { return }
        if (response.status === 'ok') {
            const topic = response.topic
            const parts: string[] = [topic.brief]
            if (topic.syntax.length > 0) { parts.push('Syntax: ' + topic.syntax.join(', ')) }
            if (topic.seeAlso.length > 0) { parts.push('See also: ' + topic.seeAlso.join(', ')) }
            useAnnounceStore.getState().pushMessage(stripMarkup(parts.join('. ')), 'assertive')
        } else if (response.status === 'multiple') {
            const titles = response.matches.map((m) => m.title).join(', ')
            const label = response.term ? `Multiple matches for "${response.term}": ${titles}` : `Help categories: ${titles}`
            useAnnounceStore.getState().pushMessage(label, 'assertive')
        }
    }, [isOpen, response])

    useEffect(() => {
        const handler = (e: KeyboardEvent) => {
            if (e.key === 'Escape' && isOpen) { closeHelp() }
        }
        document.addEventListener('keydown', handler)
        return () => { document.removeEventListener('keydown', handler) }
    }, [isOpen, closeHelp])

    return (
        <dialog
            ref={dialogRef}
            className="fixed z-50 m-auto max-w-2xl w-full max-h-[80vh] rounded-lg bg-gray-900 text-gray-100 shadow-2xl p-0 backdrop:bg-black/60 border border-gray-700"
            onClick={(e) => { if (e.target === dialogRef.current) { closeHelp() } }}
        >
            {response && (
                <div className="flex flex-col max-h-[80vh]">
                    <header className="flex items-center justify-between px-4 py-3 border-b border-gray-700 shrink-0">
                        <h2 className="text-lg font-semibold">
                            {response.status === 'ok'
                                ? response.topic.title
                                : response.status === 'multiple'
                                    ? (response.term ? `Help: "${response.term}"` : 'Help Topics')
                                    : response.status === 'no_match'
                                        ? `Help: "${response.term}"`
                                        : ''}
                        </h2>
                        <button
                            onClick={closeHelp}
                            className="text-gray-400 hover:text-gray-100 text-xl leading-none ml-4"
                            aria-label="Close help"
                        >
                            &times;
                        </button>
                    </header>
                    <div className="overflow-y-auto px-4 py-4 flex-1">
                        {response.status === 'ok' && <HelpTopicContent topic={response.topic} />}
                        {response.status === 'multiple' && (
                            <HelpDisambiguation term={response.term} matches={response.matches} />
                        )}
                        {response.status === 'no_match' && (
                            <p className="text-gray-400 text-sm">No help topic found for "{response.term}".</p>
                        )}
                    </div>
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

            <div className="text-sm leading-relaxed whitespace-pre-wrap">
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
                {term ? `Multiple topics match "${term}":` : 'Browse a category or type help [topic]:'}
            </p>
            <ul className="space-y-1">
                {matches.map((m) => (
                    <li key={m.id}>
                        <button
                            className="text-left w-full hover:bg-gray-800 rounded px-2 py-1.5 flex gap-3"
                            onClick={() => { sendCommand(`help ${m.id}`) }}
                        >
                            <span className="text-blue-400 font-mono shrink-0">{m.id}</span>
                            <span className="text-gray-300">{m.title}</span>
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
