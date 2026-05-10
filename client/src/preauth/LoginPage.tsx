import { useState, useRef, useEffect } from 'react'
import { deriveAuthBaseUrl } from '../config/clientConfig'
import { WebSocketClient } from '../connection/WebSocketClient'
import { WheelMark } from './WheelMark'
import './preauth.css'

/* ── Shared sub-components ───────────────────── */

function StepIndicator({ step }: { step: 1 | '2a' | '2b' }) {
  const onTwo = step !== 1
  const label = step === 1
    ? 'Step 1 of 2: Character name'
    : step === '2a'
      ? 'Step 2 of 2: Credentials'
      : 'Step 2 of 2: New thread'

  return (
    <nav className="step-row" aria-label={label}>
      <span className={`step-pip ${onTwo ? 'done' : 'active'}`} aria-hidden="true">
        {onTwo ? '✓' : '1'}
      </span>
      <span className={step === 1 ? 'step-label-active' : ''}>Character</span>
      <span className={`step-line ${onTwo ? 'lit' : ''}`} aria-hidden="true" />
      <span className={`step-pip ${onTwo ? 'active' : ''}`} aria-hidden="true">2</span>
      <span className={onTwo ? 'step-label-active' : ''}>
        {step === '2b' ? 'New thread' : 'Credentials'}
      </span>
    </nav>
  )
}

function LoadingDots() {
  return (
    <>
      <span className="dots-load" aria-hidden="true"><i /><i /><i /></span>
      <span className="pa-sr-only">Loading</span>
    </>
  )
}

/* ── Step 1: Name entry ──────────────────────── */

function NameStep({ onAdvance }: { onAdvance: (name: string, kind: 'returning' | 'new') => void }) {
  const [value, setValue] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const trimmed = value.trim()
    if (!trimmed) { return }

    setError('')
    setBusy(true)
    try {
      const baseUrl = deriveAuthBaseUrl()
      const res = await fetch(`${baseUrl}/auth/check?name=${encodeURIComponent(trimmed)}`)
      if (res.status === 429) {
        throw new Error('Too many attempts. Please wait a moment and try again.')
      }
      const data = await res.json()
      if (!data.nameValid) {
        throw new Error(data.error ?? 'Invalid name.')
      }
      if (data.exists) {
        onAdvance(trimmed, 'returning')
      } else {
        onAdvance(trimmed, 'new')
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.')
    } finally {
      setBusy(false)
    }
  }

  const trimmed = value.trim()
  const isDisabled = busy || !trimmed

  return (
    <div className="pa-card">
      <StepIndicator step={1} />
      <h2 className="pa-form-h">Speak your name into the loom.</h2>
      <p className="pa-form-sub">Step <span className="gold">01</span> &middot; Character</p>
      <form onSubmit={handleSubmit} aria-busy={busy}>
        <div className="pa-field">
          <label className="pa-field-label" htmlFor="pa-name">Character name</label>
          <div className="pa-input-wrap">
            <span className="pa-input-prompt" aria-hidden="true">&rsaquo;</span>
            <input
              ref={inputRef}
              id="pa-name"
              className="pa-input"
              type="text"
              placeholder="Enter a name"
              autoComplete="off"
              value={value}
              disabled={busy}
              onChange={(e) => { setValue(e.target.value) }}
            />
          </div>
        </div>
        <div className="pa-error-slot" role="alert" aria-live="polite">
          {error && (
            <>
              <span className="pa-error-icon" aria-hidden="true">!</span>
              {error}
            </>
          )}
        </div>
        <button
          className="pa-submit"
          type="submit"
          disabled={isDisabled}
          aria-disabled={isDisabled}
        >
          {busy ? <LoadingDots /> : 'Continue →'}
        </button>
      </form>
    </div>
  )
}

/* ── Step 2a: Returning character password ───── */

function ReturningStep({ name, onBack }: { name: string; onBack: () => void }) {
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!password) { return }

    setError('')
    setBusy(true)
    try {
      const baseUrl = deriveAuthBaseUrl()
      const res = await fetch(`${baseUrl}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, password }),
      })
      if (res.status === 429) {
        throw new Error('Too many attempts. Please wait a moment and try again.')
      }
      const data = await res.json()
      if (!res.ok) {
        throw new Error(data.error ?? 'Login failed')
      }
      const serverUrl = WebSocketClient.deriveServerUrl()
      if (serverUrl) {
        WebSocketClient.connect(serverUrl, data.token)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed.')
    } finally {
      setBusy(false)
    }
  }

  const isDisabled = busy || !password

  return (
    <div className="pa-card">
      <StepIndicator step="2a" />
      <h2 className="pa-form-h">Welcome back, <em>{name}</em>.</h2>
      <p className="pa-form-sub">Step <span className="gold">02</span> &middot; Credentials</p>
      <form onSubmit={handleSubmit} aria-busy={busy}>
        <div className="pa-field">
          <label className="pa-field-label" htmlFor="pa-password">Password</label>
          <div className="pa-input-wrap">
            <span className="pa-input-prompt" aria-hidden="true">&middot;</span>
            <input
              ref={inputRef}
              id="pa-password"
              className="pa-input"
              type="password"
              placeholder="Enter your password"
              autoComplete="current-password"
              value={password}
              disabled={busy}
              onChange={(e) => { setPassword(e.target.value) }}
            />
          </div>
        </div>
        <div className="pa-error-slot" role="alert" aria-live="polite">
          {error && (
            <>
              <span className="pa-error-icon" aria-hidden="true">!</span>
              {error}
            </>
          )}
        </div>
        <button
          className="pa-submit"
          type="submit"
          disabled={isDisabled}
          aria-disabled={isDisabled}
        >
          {busy ? <LoadingDots /> : 'Enter the Pattern →'}
        </button>
      </form>
      <div className="pa-back-row">
        <button
          className="pa-back-link"
          type="button"
          onClick={onBack}
          disabled={busy}
          aria-disabled={busy}
        >
          &larr; Use a different name
        </button>
      </div>
    </div>
  )
}

/* ── Step 2b: New character creation ─────────── */

function NewCharStep({ name, onBack }: { name: string; onBack: () => void }) {
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!password || !confirm) { return }
    if (password !== confirm) {
      setError('Passwords do not match.')
      return
    }

    setError('')
    setBusy(true)
    try {
      const baseUrl = deriveAuthBaseUrl()
      const res = await fetch(`${baseUrl}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, password, confirmPassword: confirm }),
      })
      if (res.status === 429) {
        throw new Error('Too many attempts. Please wait a moment and try again.')
      }
      const data = await res.json()
      if (!res.ok) {
        throw new Error(data.error ?? 'Creation failed')
      }
      const serverUrl = WebSocketClient.deriveServerUrl()
      if (serverUrl) {
        WebSocketClient.connect(serverUrl, data.token)
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Creation failed.')
    } finally {
      setBusy(false)
    }
  }

  const isDisabled = busy || !password || !confirm

  return (
    <div className="pa-card">
      <StepIndicator step="2b" />
      <div className="pa-new-badge">{name}</div>
      <h2 className="pa-form-h">Bind a new thread to the loom.</h2>
      <p className="pa-form-sub">Step <span className="gold">02</span> &middot; New thread</p>
      <form onSubmit={handleSubmit} aria-busy={busy}>
        <div className="pa-field">
          <label className="pa-field-label" htmlFor="pa-new-pw">Password</label>
          <div className="pa-input-wrap">
            <span className="pa-input-prompt" aria-hidden="true">&middot;</span>
            <input
              ref={inputRef}
              id="pa-new-pw"
              className="pa-input"
              type="password"
              placeholder="Choose a password"
              autoComplete="new-password"
              value={password}
              disabled={busy}
              onChange={(e) => { setPassword(e.target.value) }}
            />
          </div>
        </div>
        <div className="pa-field">
          <label className="pa-field-label" htmlFor="pa-confirm-pw">Confirm password</label>
          <div className="pa-input-wrap">
            <span className="pa-input-prompt" aria-hidden="true">&middot;</span>
            <input
              id="pa-confirm-pw"
              className="pa-input"
              type="password"
              placeholder="Confirm password"
              autoComplete="new-password"
              value={confirm}
              disabled={busy}
              onChange={(e) => { setConfirm(e.target.value) }}
            />
          </div>
        </div>
        <div className="pa-error-slot" role="alert" aria-live="polite">
          {error && (
            <>
              <span className="pa-error-icon" aria-hidden="true">!</span>
              {error}
            </>
          )}
        </div>
        <button
          className="pa-submit"
          type="submit"
          disabled={isDisabled}
          aria-disabled={isDisabled}
        >
          {busy ? <LoadingDots /> : 'Create & enter →'}
        </button>
      </form>
      <div className="pa-back-row">
        <button
          className="pa-back-link"
          type="button"
          onClick={onBack}
          disabled={busy}
          aria-disabled={busy}
        >
          &larr; Use a different name
        </button>
      </div>
    </div>
  )
}

/* ── Main LoginPage ──────────────────────────── */

type Step =
  | { phase: 'name' }
  | { phase: 'returning'; name: string }
  | { phase: 'new'; name: string }

export function LoginPage({ onBack }: { onBack: () => void }) {
  const [step, setStep] = useState<Step>({ phase: 'name' })

  function handleNameAdvance(name: string, kind: 'returning' | 'new') {
    if (kind === 'returning') {
      setStep({ phase: 'returning', name })
    } else {
      setStep({ phase: 'new', name })
    }
  }

  function handleStepBack() {
    setStep({ phase: 'name' })
  }

  return (
    <main className="preauth">
      <div className="pa-stage">
        <img src="/assets/tapestry-logo.png" alt="Tapestry" className="pa-login-logo" />
        <WheelMark size={72} />
        <div className="pa-fili" aria-hidden="true">&diams; &loz; &diams;</div>
        <p className="pa-page-sub">The loom remembers every thread.</p>

        <h1 className="pa-sr-only">Tapestry Login</h1>

        {step.phase === 'name' && (
          <NameStep onAdvance={handleNameAdvance} />
        )}
        {step.phase === 'returning' && (
          <ReturningStep name={step.name} onBack={handleStepBack} />
        )}
        {step.phase === 'new' && (
          <NewCharStep name={step.name} onBack={handleStepBack} />
        )}

        <div className="pa-below">
          <button type="button" onClick={onBack}>&larr; Home</button>
          <span className="dot" aria-hidden="true">&middot;</span>
          <span>
            <span className="pa-conn-led" aria-hidden="true" />
            {' '}Server online
          </span>
        </div>
      </div>
    </main>
  )
}
