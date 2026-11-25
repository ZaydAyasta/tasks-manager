import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
vi.mock('../services/api', () => ({ api: vi.fn(), configureApi: vi.fn() }))
import { api } from '../services/api'
import { useAuthStore } from './auth.store'

describe('auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('keeps only the CSRF token in memory after login', async () => {
    api.mockResolvedValue({ accessToken: null, csrfToken: 'csrf', user: { id: '1', role: 'Admin' } })
    const store = useAuthStore()

    await store.login({ email: 'a@b.c', password: 'secret' })

    expect(store.isAuthenticated).toBe(true)
    expect(store.accessToken).toBeNull()
    expect(store.csrfToken).toBe('csrf')
    expect(localStorage.getItem('nakama.access-token')).toBeNull()
  })

  it('clears the in-memory session after logout', async () => {
    api.mockResolvedValue(null)
    const store = useAuthStore()
    store.csrfToken = 'csrf'
    store.user = { role: 'Admin' }

    await store.logout()

    expect(store.isAuthenticated).toBe(false)
    expect(store.csrfToken).toBeNull()
  })

  it('retains the session for retry when the server cannot confirm logout', async () => {
    const store = useAuthStore()
    store.user = { role: 'Admin' }
    store.csrfToken = 'csrf'
    api.mockRejectedValueOnce({ status: 503 }).mockResolvedValueOnce(null)

    await expect(store.logout()).rejects.toMatchObject({ status: 503 })
    expect(store.isAuthenticated).toBe(true)
    expect(store.csrfToken).toBe('csrf')
    await store.logout()
    expect(store.isAuthenticated).toBe(false)
  })

  it('restores the cookie-backed session and removes it after a 401', async () => {
    api.mockResolvedValueOnce({ id: '1', role: 'Admin', csrfToken: 'csrf' })
    const store = useAuthStore()

    await expect(store.restoreSession()).resolves.toBe(true)

    expect(store.user).toEqual({ id: '1', role: 'Admin' })
    expect(store.csrfToken).toBe('csrf')

    store.clear()
    store.sessionChecked = false
    api.mockRejectedValueOnce({ status: 401 })

    await expect(store.restoreSession()).resolves.toBe(false)

    expect(store.user).toBeNull()
    expect(store.csrfToken).toBeNull()
  })

  it('recovers the CSRF proof once when an active cookie outlives the in-memory session', async () => {
    api.mockRejectedValueOnce({ status: 403, problem: { type: 'https://nakama/errors/csrf-invalid' } })
      .mockResolvedValueOnce({ csrfToken: 'previous-session-proof' })
      .mockResolvedValueOnce({ accessToken: null, csrfToken: 'new-session-proof', user: { id: '1', role: 'Admin' } })
    const store = useAuthStore()

    await store.login({ email: 'admin@nakama.test', password: 'fixture password' })

    expect(api.mock.calls.map(([route]) => route)).toEqual(['/api/auth/login', '/api/auth/me', '/api/auth/login'])
    expect(store.csrfToken).toBe('new-session-proof')
    expect(store.user?.id).toBe('1')
  })
})
