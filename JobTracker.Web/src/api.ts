export type AuthUser = {
  id: string
  username: string
  displayName: string
  email: string | null
  avatarKey: string
}

export type ApplicationStatus = 'Saved' | 'Applied' | 'Interview' | 'Offer' | 'Rejected' | 'Withdrawn'

export type JobApplication = {
  id: number
  companyName: string
  jobTitle: string
  location: string | null
  salary: number | null
  jobUrl: string | null
  applicationDate: string
  status: ApplicationStatus
  notes: string | null
}

export type ApplicationRequest = Omit<JobApplication, 'id'>

export type PagedResponse<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type ApplicationImportResponse = {
  importedCount: number
}

type ApiProblem = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

type CsrfResponse = {
  requestToken: string
  headerName: string
}

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

let csrfToken: CsrfResponse | null = null

async function readProblem(response: Response): Promise<never> {
  const problem = await response.json().catch(() => null) as ApiProblem | null
  const fieldMessages = problem?.errors
    ? Object.values(problem.errors).flat().join(' ')
    : ''
  throw new ApiError(
    fieldMessages || problem?.detail || problem?.title || `Request failed (${response.status}).`,
    response.status,
  )
}

async function getCsrfToken(): Promise<CsrfResponse> {
  if (csrfToken) return csrfToken

  let response: Response
  try {
    response = await fetch('/api/auth/csrf', {
      credentials: 'include',
      cache: 'no-store',
    })
  } catch {
    throw new Error('Unable to reach JobTracker. Check your connection and try again.')
  }

  if (!response.ok) return readProblem(response)
  csrfToken = await response.json() as CsrfResponse
  return csrfToken
}

export async function refreshCsrfToken(): Promise<void> {
  csrfToken = null
  await getCsrfToken()
}

export type ApiRequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: object | Blob
  contentType?: string
}

export async function apiRequest<T>(
  path: string,
  { method = 'GET', body, contentType }: ApiRequestOptions = {},
): Promise<T> {
  const headers: Record<string, string> = {}
  const rawBody = body instanceof Blob
  if (body) {
    headers['Content-Type'] = rawBody
      ? contentType ?? body.type ?? 'application/octet-stream'
      : 'application/json'
  }

  if (method !== 'GET') {
    const csrf = await getCsrfToken()
    headers[csrf.headerName] = csrf.requestToken
  }

  let response: Response
  try {
    response = await fetch(path, {
      method,
      credentials: 'include',
      cache: 'no-store',
      headers,
      body: body ? (rawBody ? body : JSON.stringify(body)) : undefined,
    })
  } catch {
    throw new Error('Unable to reach JobTracker. Check your connection and try again.')
  }

  if (!response.ok) {
    if (method !== 'GET' && response.status === 400) csrfToken = null
    return readProblem(response)
  }

  return response.status === 204 ? undefined as T : response.json() as Promise<T>
}

export async function getCurrentUser(): Promise<AuthUser | null> {
  try {
    return await apiRequest<AuthUser>('/api/auth/me')
  } catch (error) {
    if (error instanceof ApiError && error.status === 401) return null
    throw error
  }
}

export type ApplicationFilters = {
  search: string
  status: ApplicationStatus | ''
  location: string
  from: string
  to: string
  newestFirst: boolean
}

export const emptyFilters: ApplicationFilters = {
  search: '',
  status: '',
  location: '',
  from: '',
  to: '',
  newestFirst: true,
}

export async function getApplications(
  page: number,
  filters: ApplicationFilters,
): Promise<PagedResponse<JobApplication>> {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: '20',
    newestFirst: String(filters.newestFirst),
  })
  if (filters.search.trim()) query.set('search', filters.search.trim())
  if (filters.status) query.set('status', filters.status)
  if (filters.location.trim()) query.set('location', filters.location.trim())
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  return apiRequest<PagedResponse<JobApplication>>(`/api/applications/?${query}`)
}

export async function getApplication(id: number): Promise<JobApplication> {
  return apiRequest<JobApplication>(`/api/applications/${id}`)
}

export async function createApplication(request: ApplicationRequest): Promise<JobApplication> {
  return apiRequest<JobApplication>('/api/applications/', { method: 'POST', body: request })
}

export async function updateApplication(
  id: number,
  request: ApplicationRequest,
): Promise<JobApplication> {
  return apiRequest<JobApplication>(`/api/applications/${id}`, { method: 'PUT', body: request })
}

export async function importApplications(file: File): Promise<ApplicationImportResponse> {
  return apiRequest<ApplicationImportResponse>('/api/applications/import', {
    method: 'POST',
    body: file,
    contentType: 'text/csv',
  })
}
