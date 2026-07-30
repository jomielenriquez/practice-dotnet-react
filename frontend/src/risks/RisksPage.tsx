import { useCallback, useEffect, useState } from 'react'
import './RisksPage.css'
import { apiFetch } from '../api/client'
import type { Risk, Severity } from '../api/types'

/**
 * Explicit loading / error / success states rather than a bare nullable, so
 * "no risks yet" and "not loaded yet" can never be confused for each other.
 */
type RiskListState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'success'; risks: Risk[] }

/**
 * Severity band → card class. A `Record` rather than a
 * `` `sev-${severity.toLowerCase()}` `` template on purpose: if the backend
 * ever gains a band, this fails the build instead of quietly rendering an
 * unstyled card.
 */
const severityClass: Record<Severity, string> = {
  Low: 'sev-low',
  Medium: 'sev-medium',
  High: 'sev-high',
  Critical: 'sev-critical',
}

function RisksPage() {
  const [state, setState] = useState<RiskListState>({ status: 'loading' })
  const [attempt, setAttempt] = useState(0)

  const retry = useCallback(() => setAttempt((n) => n + 1), [])

  useEffect(() => {
    const controller = new AbortController()
    setState({ status: 'loading' })

    // `apiFetch` does not read the RFC 7807 body, so a failure surfaces as
    // "GET /api/risks failed: 503 …". Fine here — without `?status=` there is
    // no 400 path — but the capture form will need the `errors` map, and that
    // is where `apiFetch` grows a ProblemDetails parse.
    apiFetch<Risk[]>('/api/risks', { signal: controller.signal })
      .then((risks) => setState({ status: 'success', risks }))
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
      <p className="subtitle">
        Ordered worst first — highest score at the top.
      </p>

      {/*
        `aria-busy` but deliberately no `aria-live`: the region holds the whole
        register, and a live region here would read every card aloud on load.
        The transient messages below carry the live semantics instead.
      */}
      <section className="panel" aria-busy={state.status === 'loading'}>
        {state.status === 'loading' && (
          <p className="muted" role="status">
            Loading the register…
          </p>
        )}

        {state.status === 'error' && (
          <div className="error" role="alert">
            <h2>Could not load</h2>
            <p>{state.message}</p>
            <button type="button" onClick={retry}>
              Try again
            </button>
          </div>
        )}

        {state.status === 'success' && state.risks.length === 0 && (
          <p className="muted" role="status">
            No risks logged yet. Once risks are captured they will appear here,
            worst first.
          </p>
        )}

        {state.status === 'success' && state.risks.length > 0 && (
          <ul className="risk-list">
            {/*
              Rendered in the order the API returned them. The register's
              ordering — Score DESC, CreatedUtc DESC, Id DESC — is done in SQL
              and carried by two covering indexes, so sorting again here would
              only risk disagreeing with it.
            */}
            {state.risks.map((risk) => (
              <li
                key={risk.id}
                className={`risk-card ${severityClass[risk.severity]}`}
              >
                <span className="risk-badge">{risk.severity}</span>
                <h2>{risk.title}</h2>

                {risk.description !== null && (
                  <p className="risk-description">{risk.description}</p>
                )}

                <dl className="risk-meta">
                  <div>
                    <dt>Owner</dt>
                    <dd>{risk.owner}</dd>
                  </div>
                  <div>
                    <dt>Likelihood</dt>
                    <dd>{risk.likelihood}</dd>
                  </div>
                  <div>
                    <dt>Impact</dt>
                    <dd>{risk.impact}</dd>
                  </div>
                  <div>
                    <dt>Score</dt>
                    <dd>{risk.score}</dd>
                  </div>
                  <div>
                    <dt>Status</dt>
                    <dd>{risk.status}</dd>
                  </div>
                </dl>
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  )
}

export default RisksPage
