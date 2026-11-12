import { useEffect, useState, type FormEvent } from 'react'
import './App.css'
import { AuthScreen, type AuthMode } from './AuthScreen'
import {
  ApiError,
  apiRequest,
  createApplication,
  deleteApplication,
  emptyFilters,
  getApplication,
  getApplications,
  getCurrentUser,
  importApplications,
  refreshCsrfToken,
  updateApplication,
  type ApplicationFilters,
  type ApplicationRequest,
  type ApplicationStatus,
  type AuthUser,
  type JobApplication,
  type PagedResponse,
} from './api'

type ApplicationDraft = {
  companyName: string
  jobTitle: string
  location: string
  salary: string
  jobUrl: string
  applicationDate: string
  status: ApplicationStatus
  notes: string
}

const statuses: ApplicationStatus[] = ['Saved', 'Applied', 'Interview', 'Offer', 'Rejected', 'Withdrawn']

function localDate(): string {
  const now = new Date()
  const offset = now.getTimezoneOffset() * 60_000
  return new Date(now.getTime() - offset).toISOString().slice(0, 10)
}

function emptyDraft(): ApplicationDraft {
  return {
    companyName: '',
    jobTitle: '',
    location: '',
    salary: '',
    jobUrl: '',
    applicationDate: localDate(),
    status: 'Saved',
    notes: '',
  }
}

function draftFrom(application: JobApplication): ApplicationDraft {
  return {
    companyName: application.companyName,
    jobTitle: application.jobTitle,
    location: application.location ?? '',
    salary: application.salary?.toString() ?? '',
    jobUrl: application.jobUrl ?? '',
    applicationDate: application.applicationDate.slice(0, 10),
    status: application.status,
    notes: application.notes ?? '',
  }
}

function requestFrom(draft: ApplicationDraft): ApplicationRequest {
  return {
    companyName: draft.companyName.trim(),
    jobTitle: draft.jobTitle.trim(),
    location: draft.location.trim() || null,
    salary: draft.salary === '' ? null : Number(draft.salary),
    jobUrl: draft.jobUrl.trim() || null,
    applicationDate: draft.applicationDate,
    status: draft.status,
    notes: draft.notes.trim() || null,
  }
}

const readModeFromPath = (): AuthMode =>
  window.location.pathname === '/register' ? 'register' : 'login'

function App() {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [mode, setMode] = useState<AuthMode>(readModeFromPath)
  const [sessionLoading, setSessionLoading] = useState(true)
  const [sessionError, setSessionError] = useState('')
  const [applications, setApplications] = useState<PagedResponse<JobApplication> | null>(null)
  const [applicationsLoading, setApplicationsLoading] = useState(false)
  const [applicationsError, setApplicationsError] = useState('')
  const [page, setPage] = useState(1)
  const [filterInput, setFilterInput] = useState<ApplicationFilters>(emptyFilters)
  const [filters, setFilters] = useState<ApplicationFilters>(emptyFilters)
  const hasFilters = filters.search !== '' || filters.status !== '' || filters.location !== ''
    || filters.from !== '' || filters.to !== ''
  const [filterError, setFilterError] = useState('')
  const resetFilters = () => {
    setFilterError('')
    setFilterInput(emptyFilters)
    setFilters(emptyFilters)
  }
  const [refreshCount, setRefreshCount] = useState(0)
  const [draft, setDraft] = useState<ApplicationDraft | null>(null)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [formError, setFormError] = useState('')
  const [saving, setSaving] = useState(false)
  const [loadingEditId, setLoadingEditId] = useState<number | null>(null)
  const [changingStatusId, setChangingStatusId] = useState<number | null>(null)
  const [deletingId, setDeletingId] = useState<number | null>(null)
  const [importing, setImporting] = useState(false)
  const [importMessage, setImportMessage] = useState('')
  const [importError, setImportError] = useState('')

  useEffect(() => {
    let active = true
    getCurrentUser()
      .then((currentUser) => {
        if (active) setUser(currentUser)
      })
      .catch((error: unknown) => {
        if (active) {
          setSessionError(error instanceof Error ? error.message : 'Unable to load your session.')
        }
      })
      .finally(() => {
        if (active) setSessionLoading(false)
      })

    const handlePopState = () => setMode(readModeFromPath())
    window.addEventListener('popstate', handlePopState)
    return () => {
      active = false
      window.removeEventListener('popstate', handlePopState)
    }
  }, [])

  useEffect(() => {
    if (!user) return

    let active = true
    const loadApplications = async () => {
      await Promise.resolve()
      if (!active) return

      setApplicationsLoading(true)
      setApplicationsError('')
      try {
        const result = await getApplications(page, filters)
        if (active) setApplications(result)
      } catch (error) {
        if (!active) return
        if (error instanceof ApiError && error.status === 401) {
          setApplications(null)
          setDraft(null)
          setUser(null)
          setSessionError('Your session has expired. Please sign in again.')
        } else {
          setApplicationsError(error instanceof Error ? error.message : 'Unable to load applications.')
        }
      } finally {
        if (active) setApplicationsLoading(false)
      }
    }
    void loadApplications()

    return () => {
      active = false
    }
  }, [user, page, filters, refreshCount])

  const navigate = (nextMode: AuthMode | 'home') => {
    const path = nextMode === 'home' ? '/' : `/${nextMode}`
    window.history.pushState({}, '', path)
    setMode(nextMode === 'home' ? 'login' : nextMode)
  }

  const handleAuthenticated = async (authenticatedUser: AuthUser) => {
    setSessionError('')
    setUser(authenticatedUser)
    setPage(1)
    resetFilters()
    window.history.pushState({}, '', '/')
    try {
      await refreshCsrfToken()
    } catch {
      setSessionError('You are signed in, but a security token could not be refreshed. Try again before your next account action.')
    }
  }

  const handleLogout = async () => {
    setSessionError('')
    try {
      await apiRequest<void>('/api/auth/logout', { method: 'POST' })
      setUser(null)
      setApplications(null)
      setDraft(null)
      resetFilters()
      setPage(1)
      navigate('login')
      try {
        await refreshCsrfToken()
      } catch {
        setSessionError('You have signed out, but a security token could not be refreshed. Try again before signing in.')
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setApplications(null)
        setDraft(null)
        setUser(null)
        navigate('login')
        setSessionError('Your session has expired. Please sign in again.')
        return
      }
      setSessionError(error instanceof Error ? error.message : 'Unable to sign out. Please try again.')
    }
  }

  const openCreateForm = () => {
    setFormError('')
    setEditingId(null)
    setDraft(emptyDraft())
  }

  const openEditForm = async (application: JobApplication) => {
    setFormError('')
    setLoadingEditId(application.id)
    try {
      const latest = await getApplication(application.id)
      setEditingId(latest.id)
      setDraft(draftFrom(latest))
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setApplications(null)
        setDraft(null)
        setUser(null)
        setSessionError('Your session has expired. Please sign in again.')
      } else {
        setApplicationsError(error instanceof Error ? error.message : 'Unable to load this application.')
      }
    } finally {
      setLoadingEditId(null)
    }
  }

  const handleApplicationError = (error: unknown, fallback: string) => {
    if (error instanceof ApiError && error.status === 401) {
      setApplications(null)
      setDraft(null)
      setUser(null)
      setSessionError('Your session has expired. Please sign in again.')
      return
    }
    setFormError(error instanceof Error ? error.message : fallback)
  }

  const handleSave = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!draft) return
    setFormError('')
    setSaving(true)
    try {
      if (editingId === null) {
        await createApplication(requestFrom(draft))
        resetFilters()
      } else {
        await updateApplication(editingId, requestFrom(draft))
      }
      setDraft(null)
      setEditingId(null)
      setPage(1)
      setRefreshCount((count) => count + 1)
    } catch (error) {
      handleApplicationError(error, 'Unable to save this application.')
    } finally {
      setSaving(false)
    }
  }

  const handleImport = async (file: File) => {
    setImportMessage('')
    setImportError('')
    setImporting(true)
    try {
      const result = await importApplications(file)
      setImportMessage(`Imported ${result.importedCount} ${result.importedCount === 1 ? 'application' : 'applications'}.`)
      resetFilters()
      setPage(1)
      setRefreshCount((count) => count + 1)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setApplications(null)
        setDraft(null)
        setUser(null)
        setSessionError('Your session has expired. Please sign in again.')
      } else {
        setImportError(error instanceof Error ? error.message : 'Unable to import applications.')
      }
    } finally {
      setImporting(false)
    }
  }

  const handleStatusChange = async (application: JobApplication, nextStatus: ApplicationStatus) => {
    if (nextStatus === application.status) return
    setApplicationsError('')
    setChangingStatusId(application.id)
    try {
      await updateApplication(application.id, {
        companyName: application.companyName,
        jobTitle: application.jobTitle,
        location: application.location,
        salary: application.salary,
        jobUrl: application.jobUrl,
        applicationDate: application.applicationDate,
        status: nextStatus,
        notes: application.notes,
      })
      setRefreshCount((count) => count + 1)
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setApplications(null)
        setDraft(null)
        setUser(null)
        setSessionError('Your session has expired. Please sign in again.')
      } else {
        setApplicationsError(error instanceof Error ? error.message : 'Unable to update application status.')
      }
    } finally {
      setChangingStatusId(null)
    }
  }

  const handleDelete = async (application: JobApplication) => {
    const confirmed = window.confirm(
      `Delete your application for ${application.jobTitle} at ${application.companyName}?`
    )

    if (!confirmed) return

    setApplicationsError('')
    setDeletingId(application.id)

    try {
      await deleteApplication(application.id)

      if (applications?.items.length === 1 && page > 1) {
        setPage((current) => current - 1)
      } else {
        setRefreshCount((count) => count + 1)
      }
    } catch (error) {
      setApplicationsError(
        error instanceof Error ? error.message : 'Unable to delete application.'
      )
    } finally {
      setDeletingId(null)
    }
  }

  const applyFilters = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (filterInput.from && filterInput.to && filterInput.from > filterInput.to) {
      setFilterError('"Applied from" must not be later than "Applied to".')
      return
    }
    setFilterError('')
    setPage(1)
    setFilters(filterInput)
  }

  const clearFilters = () => {
    resetFilters()
    setPage(1)
  }

  if (sessionLoading) {
    return <main className="session-loading" aria-live="polite">Loading your workspace…</main>
  }

  if (!user) {
    return (
      <AuthScreen
        key={sessionError}
        mode={mode}
        initialError={sessionError}
        onModeChange={navigate}
        onAuthenticated={handleAuthenticated}
      />
    )
  }

  return (
    <div className="workspace">
      <aside className="sidebar" aria-label="Main navigation">
        <a className="brand" href="#home" aria-label="JobTracker home">
          <span className="brand-mark" aria-hidden="true">J</span>
          <span>JobTracker</span>
        </a>

        <p className="nav-label">WORKSPACE</p>
        <nav className="nav-links">
          <a className="nav-link is-active" href="#overview" aria-current="page">
            <span className="nav-dot" aria-hidden="true" />Overview
          </a>
          <a className="nav-link" href="#applications">
            <span className="nav-dot" aria-hidden="true" />Applications
          </a>
        </nav>

        <div className="sidebar-foot">
          <span className="connection-dot" aria-hidden="true" />
          <span>Signed in as {user.displayName}</span>
        </div>
      </aside>

      <main className="main-panel" id="home">
        <header className="topbar">
          <p>PERSONAL WORKSPACE</p>
          <div className="topbar-account">
            <span className="topbar-date">@{user.username}</span>
            <button className="text-button" type="button" onClick={handleLogout}>Sign out</button>
          </div>
        </header>

        <section className="page-content" id="overview" aria-labelledby="page-title">
          {sessionError && <p className="notice-error" role="alert">{sessionError}</p>}
          <div className="page-heading">
            <div>
              <p className="eyebrow">OVERVIEW</p>
              <h1 id="page-title">Your next move,<br /><em>well organized.</em></h1>
              <p className="page-intro">A clear view of your job search starts here, {user.displayName}.</p>
            </div>
            <span className="heading-index" aria-hidden="true">01</span>
          </div>

          <section className="summary-row" aria-label="Application summary">
            <article className="summary-item">
              <span className="summary-label">TOTAL APPLICATIONS</span>
              <strong>{applications?.totalCount ?? '—'}</strong>
            </article>
            <article className="summary-item">
              <span className="summary-label">IN PROGRESS ON PAGE</span>
              <strong>{applications?.items.filter((application) => ['Applied', 'Interview'].includes(application.status)).length ?? '—'}</strong>
            </article>
            <article className="summary-item">
              <span className="summary-label">INTERVIEWS ON PAGE</span>
              <strong>{applications?.items.filter((application) => application.status === 'Interview').length ?? '—'}</strong>
            </article>
          </section>

          <section className="applications-section" id="applications" aria-labelledby="applications-title">
            <div className="section-heading">
              <div>
                <p className="eyebrow">YOUR TRACKER</p>
                <h2 id="applications-title">Applications</h2>
              </div>
              <div className="application-toolbar">
                <label className={`import-button${importing ? ' is-disabled' : ''}`}>
                  {importing ? 'Importing…' : 'Import CSV'}
                  <input
                    accept=".csv,text/csv"
                    aria-label="Import applications from CSV"
                    className="file-input"
                    disabled={importing}
                    onChange={(event) => {
                      const file = event.currentTarget.files?.[0]
                      event.currentTarget.value = ''
                      if (file) void handleImport(file)
                    }}
                    type="file"
                  />
                </label>
                <button className="add-button" type="button" onClick={openCreateForm}>+ Add application</button>
              </div>
            </div>
            <p className="import-hint">CSV columns: CompanyName, JobTitle, Location, Salary, JobUrl, ApplicationDate, Status, Notes</p>
            {importMessage && <p className="form-success" role="status">{importMessage}</p>}
            {importError && <p className="form-error" role="alert">{importError}</p>}

            {draft && (
              <ApplicationForm
                draft={draft}
                editing={editingId !== null}
                error={formError}
                saving={saving}
                onCancel={() => {
                  setDraft(null)
                  setEditingId(null)
                  setFormError('')
                }}
                onChange={setDraft}
                onSubmit={handleSave}
              />
            )}

            <form className="application-filters" onSubmit={applyFilters}>
              <label className="filter-search">
                <span className="visually-hidden">Search applications</span>
                <input
                  onChange={(event) => setFilterInput((current) => ({ ...current, search: event.target.value }))}
                  placeholder="Search company or job title"
                  value={filterInput.search}
                />
              </label>
              <label className="filter-status">
                <span className="visually-hidden">Filter by status</span>
                <select value={filterInput.status} onChange={(event) => setFilterInput((current) => ({ ...current, status: event.target.value as ApplicationStatus | '' }))}>
                  <option value="">All statuses</option>
                  {statuses.map((status) => <option key={status} value={status}>{status}</option>)}
                </select>
              </label>
              <label className="filter-status">
                <span className="visually-hidden">Filter by location</span>
                <input
                  onChange={(event) => setFilterInput((current) => ({ ...current, location: event.target.value }))}
                  placeholder="Location"
                  value={filterInput.location}
                />
              </label>
              <label className="filter-status">
                <span className="visually-hidden">Applied from</span>
                <input
                  aria-label="Applied from"
                  onChange={(event) => setFilterInput((current) => ({ ...current, from: event.target.value }))}
                  title="Applied from"
                  type="date"
                  value={filterInput.from}
                />
              </label>
              <label className="filter-status">
                <span className="visually-hidden">Applied to</span>
                <input
                  aria-label="Applied to"
                  onChange={(event) => setFilterInput((current) => ({ ...current, to: event.target.value }))}
                  title="Applied to"
                  type="date"
                  value={filterInput.to}
                />
              </label>
              <label className="filter-status">
                <span className="visually-hidden">Sort order</span>
                <select
                  onChange={(event) => setFilterInput((current) => ({ ...current, newestFirst: event.target.value === 'newest' }))}
                  value={filterInput.newestFirst ? 'newest' : 'oldest'}
                >
                  <option value="newest">Newest first</option>
                  <option value="oldest">Oldest first</option>
                </select>
              </label>
              <button className="filter-button" type="submit">Filter</button>
              <button className="filter-button" onClick={clearFilters} type="button">Clear</button>
            </form>

            {filterError && <p className="notice-error" role="alert">{filterError}</p>}
            {applicationsError && <p className="notice-error" role="alert">{applicationsError}</p>}
            {applicationsLoading && <p className="list-message" aria-live="polite">Loading applications…</p>}

            {!applicationsLoading && applications && applications.items.length === 0 && (
              <div className="empty-state">
                <span className="empty-mark" aria-hidden="true">+</span>
                <h3>{hasFilters ? 'No matching applications' : 'Your list starts here'}</h3>
                <p>{hasFilters ? 'Try changing your search or filters.' : 'Add your first role to keep your job search organized.'}</p>
                {!hasFilters &&  <button className="text-button" type="button" onClick={openCreateForm}>Add your first application</button>}
              </div>
            )}

            {!applicationsLoading && applications && applications.items.length > 0 && (
              <>
                <div className="applications-table-wrap">
                  <table className="applications-table">
                    <thead>
                      <tr>
                        <th scope="col">Role</th>
                        <th scope="col">Applied</th>
                        <th scope="col">Status</th>
                        <th scope="col"><span className="visually-hidden">Actions</span></th>
                      </tr>
                    </thead>
                    <tbody>
                      {applications.items.map((application) => (
                        <tr key={application.id}>
                          <td>
                            <strong>{application.jobTitle}</strong>
                            <span className="application-company">{application.companyName}{application.location ? ` · ${application.location}` : ''}</span>
                          </td>
                          <td>{new Date(`${application.applicationDate.slice(0, 10)}T00:00:00`).toLocaleDateString()}</td>
                          <td>
                            <label className="status-control">
                              <span className="visually-hidden">Status for {application.jobTitle} at {application.companyName}</span>
                              <select
                                disabled={changingStatusId === application.id}
                                onChange={(event) => void handleStatusChange(application, event.target.value as ApplicationStatus)}
                                value={application.status}
                              >
                                {statuses.map((status) => <option key={status} value={status}>{status}</option>)}
                              </select>
                            </label>
                          </td>
                          <td className="application-actions">
                            <button
                              className="text-button"
                              disabled={loadingEditId === application.id}
                              onClick={() => void openEditForm(application)}
                              type="button"
                            >
                              {loadingEditId === application.id ? 'Loading…' : 'Edit'}
                            </button>
                            <button
                              className="text-button"
                              disabled={deletingId === application.id}
                              onClick={() => void handleDelete(application)}
                              type="button"
                            >
                              {deletingId === application.id ? 'Deleting…' : 'Delete'}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <div className="pagination">
                  <span>{applications.totalCount} {applications.totalCount === 1 ? 'application' : 'applications'}</span>
                  <div>
                    <button className="filter-button" disabled={page <= 1} onClick={() => setPage((current) => current - 1)} type="button">Previous</button>
                    <span>Page {applications.page} of {Math.max(1, applications.totalPages)}</span>
                    <button className="filter-button" disabled={page >= applications.totalPages} onClick={() => setPage((current) => current + 1)} type="button">Next</button>
                  </div>
                </div>
              </>
            )}
          </section>
        </section>
      </main>
    </div>
  )
}

type ApplicationFormProps = {
  draft: ApplicationDraft
  editing: boolean
  error: string
  saving: boolean
  onCancel: () => void
  onChange: (draft: ApplicationDraft) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

function ApplicationForm({
  draft,
  editing,
  error,
  saving,
  onCancel,
  onChange,
  onSubmit,
}: ApplicationFormProps) {
  const update = <K extends keyof ApplicationDraft>(key: K, value: ApplicationDraft[K]) =>
    onChange({ ...draft, [key]: value })

  return (
    <section className="application-editor" aria-labelledby="application-form-title">
      <div className="editor-heading">
        <div>
          <p className="eyebrow">{editing ? 'UPDATE YOUR TRACKER' : 'NEW OPPORTUNITY'}</p>
          <h3 id="application-form-title">{editing ? 'Edit application' : 'Add an application'}</h3>
        </div>
        <button className="text-button" type="button" onClick={onCancel}>Cancel</button>
      </div>
      <form className="application-form" onSubmit={onSubmit}>
        <label className="form-field">
          <span>Company</span>
          <input maxLength={200} onChange={(event) => update('companyName', event.target.value)} required value={draft.companyName} />
        </label>
        <label className="form-field">
          <span>Job title</span>
          <input maxLength={200} onChange={(event) => update('jobTitle', event.target.value)} required value={draft.jobTitle} />
        </label>
        <label className="form-field">
          <span>Location</span>
          <input maxLength={200} onChange={(event) => update('location', event.target.value)} value={draft.location} />
        </label>
        <label className="form-field">
          <span>Salary</span>
          <input min="0" onChange={(event) => update('salary', event.target.value)} step="0.01" type="number" value={draft.salary} />
        </label>
        <label className="form-field">
          <span>Application date</span>
          <input onChange={(event) => update('applicationDate', event.target.value)} required type="date" value={draft.applicationDate} />
        </label>
        <label className="form-field">
          <span>Status</span>
          <select onChange={(event) => update('status', event.target.value as ApplicationStatus)} value={draft.status}>
            {statuses.map((status) => <option key={status} value={status}>{status}</option>)}
          </select>
        </label>
        <label className="form-field editor-wide">
          <span>Job posting URL</span>
          <input maxLength={2048} onChange={(event) => update('jobUrl', event.target.value)} type="url" value={draft.jobUrl} />
        </label>
        <label className="form-field editor-wide">
          <span>Notes</span>
          <textarea maxLength={5000} onChange={(event) => update('notes', event.target.value)} rows={3} value={draft.notes} />
        </label>
        {error && <p className="form-error editor-wide" role="alert">{error}</p>}
        <div className="editor-actions editor-wide">
          <button className="submit-button" disabled={saving} type="submit">
            {saving ? 'Saving…' : editing ? 'Save changes' : 'Add application'}
          </button>
          <button className="filter-button" disabled={saving} onClick={onCancel} type="button">Cancel</button>
        </div>
      </form>
    </section>
  )
}

export default App
