import { useEffect, useState } from 'react'
import { getApplicationStatistics, type ApplicationStatistics, type ApplicationStatus } from './api'

export function ApplicationSummary({ statuses }: { statuses: readonly ApplicationStatus[] }) {
  const [statistics, setStatistics] = useState<ApplicationStatistics | null>(null)
  const [error, setError] = useState('')
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let active = true

    getApplicationStatistics()
      .then((result) => {
        if (active) setStatistics(result)
      })
      .catch(() => {
        if (active) setError('Unable to load application totals.')
      })

    return () => {
      active = false
    }
  }, [attempt])

  return (
    <section aria-labelledby="application-summary-title" className="application-summary">
      <h2 id="application-summary-title">Application summary</h2>
      {error && (
        <p className="notice-error" role="alert">
          {error}{' '}
          <button className="text-button" type="button" onClick={() => {
            setError('')
            setAttempt((count) => count + 1)
          }}>Retry</button>
        </p>
      )}
      <dl className="summary-row" aria-busy={!statistics && !error}>
        <div className="summary-item summary-total">
          <dt className="summary-label">Total applications</dt>
          <dd>{statistics?.totalCount ?? '—'}</dd>
        </div>
        {statuses.map((status) => (
          <div className="summary-item" key={status}>
            <dt className="summary-label">{status}</dt>
            <dd>{statistics?.byStatus[status] ?? '—'}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
