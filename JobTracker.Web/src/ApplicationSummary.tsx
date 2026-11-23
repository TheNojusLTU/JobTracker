import { useEffect, useState } from 'react'
import { getApplicationStatistics, type ApplicationStatistics, type ApplicationStatus } from './api'

const percent = new Intl.NumberFormat(undefined, { style: 'percent', maximumFractionDigits: 1 })
const monthAndYear = new Intl.DateTimeFormat(undefined, { month: 'long', year: 'numeric', timeZone: 'UTC' })

function formatMonth(year: number, month: number): string {
  const date = new Date(0)
  date.setUTCFullYear(year, month - 1, 1)
  return monthAndYear.format(date)
}

function formatRate(rate: number | null | undefined): string {
  if (rate === undefined) return '—'
  return rate === null ? 'N/A' : percent.format(rate)
}

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

      <h3>Interviews and offers</h3>
      <dl className="summary-row conversion-row" aria-busy={!statistics && !error}>
        <div className="summary-item">
          <dt className="summary-label">Reached interview</dt>
          <dd>{statistics?.interviewCount ?? '—'}</dd>
        </div>
        <div className="summary-item">
          <dt className="summary-label">Reached offer</dt>
          <dd>{statistics?.offerCount ?? '—'}</dd>
        </div>
        <div className="summary-item">
          <dt className="summary-label">Application to interview rate</dt>
          <dd>{formatRate(statistics?.applicationToInterviewRate)}</dd>
        </div>
        <div className="summary-item">
          <dt className="summary-label">Interview to offer rate</dt>
          <dd>{formatRate(statistics?.interviewToOfferRate)}</dd>
        </div>
      </dl>

      <h3 id="monthly-applications-title">Applications per month</h3>
      {statistics && (statistics.byMonth.length === 0 ? (
        <p>No applications to summarize yet.</p>
      ) : (
        <div className="applications-table-wrap">
          <table className="applications-table monthly-statistics" aria-labelledby="monthly-applications-title">
            <thead>
              <tr><th scope="col">Month</th><th scope="col">Applications</th></tr>
            </thead>
            <tbody>
              {statistics.byMonth.map(({ year, month, count }) => (
                <tr key={`${year}-${month}`}>
                  <td>{formatMonth(year, month)}</td>
                  <td>{count}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </section>
  )
}
