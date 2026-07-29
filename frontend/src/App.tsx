import { useCallback, useEffect, useState } from 'react'
import './App.css'
import { apiFetch } from './api/client'
import type { HelloResponse } from './api/types'

/**
 * Explicit loading / error / success states rather than a bare nullable.
 * The Risk Register list will need exactly this shape, so it is worth setting
 * the pattern here.
 */
type RequestState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'success'; data: HelloResponse }

function App() {
  const [state, setState] = useState<RequestState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  useEffect(() => {
    const controller = new AbortController()
    setState({ status: 'loading' })

    apiFetch<HelloResponse>('/api/hello', { signal: controller.signal })
      .then((data) => setState({ status: 'success', data }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        setState({
          status: 'error',
          message:
            error instanceof Error ? error.message : 'Something went wrong.',
        })
      })

    return () => controller.abort()
  }, [attempt])

  return (
    <main className="page">
      <h1>Risk Register</h1>
      <p className="subtitle">
        Scaffold check — this greeting comes from the .NET API via the Vite
        proxy.
      </p>

      <section className="panel" aria-live="polite" aria-busy={state.status === 'loading'}>
        {state.status === 'loading' && (
          <p className="muted">Contacting the API…</p>
        )}

        {state.status === 'error' && (
          <div className="error">
            <h2>Could not load</h2>
            <p>{state.message}</p>
            <button type="button" onClick={retry}>
              Try again
            </button>
          </div>
        )}

        {state.status === 'success' && (
          <div className="success">
            <p className="message">{state.data.message}</p>
            <p className="muted">
              Server time:{' '}
              <time dateTime={state.data.utcNow}>
                {new Date(state.data.utcNow).toLocaleString()}
              </time>
            </p>
            <button type="button" onClick={retry}>
              Call again
            </button>
          </div>
        )}
      </section>
    </main>
  )
}

export default App
