import type {
  EntityMeta, FieldMeta, LoginResponse, RecordPage, RecordRow, User
} from '~/types'

/**
 * Single API surface. In dev the Vite proxy forwards /api to the ASP.NET Core
 * server; in production /api is rewritten by IIS to the INFRA-CAP-api application.
 * Both cases are same-origin, so no CORS preflight is involved.
 */
const TOKEN_KEY = 'infra-cap.token'

export const useToken = () => useState<string | null>('auth-token', () => null)
export const useUser = () => useState<User | null>('auth-user', () => null)

export function readStoredToken(): string | null {
  if (import.meta.server) return null
  return localStorage.getItem(TOKEN_KEY)
}

export function writeStoredToken(token: string | null) {
  if (import.meta.server) return
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

function authHeaders(): Record<string, string> {
  const token = useToken().value ?? readStoredToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function request<T>(path: string, opts: { method?: string; body?: any } = {}): Promise<T> {
  const config = useRuntimeConfig()
  const res = await $fetch.raw<T>(`${config.public.apiBase}${path}`, {
    method: (opts.method || 'GET') as any,
    body: opts.body,
    headers: authHeaders()
  })
  return res._data as T
}

// ---- auth ----
export const apiLogin = (username: string, password: string) =>
  request<LoginResponse>('/auth/login', { method: 'POST', body: { username, password } })

export const apiMe = () => request<User>('/auth/me')

// ---- entities / records ----
export const apiListEntities = (all = false) =>
  request<EntityMeta[]>(`/entities${all ? '?all=true' : ''}`)

export const apiGetEntity = (id: number) => request<EntityMeta>(`/entities/${id}`)

export const apiListRecords = (entityId: number, params: Record<string, any> = {}) => {
  const qs = new URLSearchParams(
    Object.entries(params)
      .filter(([, v]) => v !== undefined && v !== null && v !== '')
      .map(([k, v]) => [k, String(v)])
  ).toString()
  return request<RecordPage>(`/records/${entityId}${qs ? `?${qs}` : ''}`)
}

export const apiCreateRecord = (entityId: number, values: Record<string, any>) =>
  request<RecordRow>(`/records/${entityId}`, { method: 'POST', body: { values } })

export const apiUpdateRecord = (entityId: number, recordId: number, values: Record<string, any>) =>
  request<RecordRow>(`/records/${entityId}/${recordId}`, { method: 'PUT', body: { values } })

export const apiDeleteRecord = (entityId: number, recordId: number) =>
  request<void>(`/records/${entityId}/${recordId}`, { method: 'DELETE' })

// ---- users (admin) ----
export const apiListUsers = () => request<User[]>('/users')
export const apiListRoles = () => request<{ id: number; name: string; description: string }[]>('/users/roles')
export const apiCreateUser = (body: any) => request<User>('/users', { method: 'POST', body })
export const apiUpdateUser = (id: number, body: any) => request<User>(`/users/${id}`, { method: 'PUT', body })
export const apiDeleteUser = (id: number) => request<void>(`/users/${id}`, { method: 'DELETE' })
