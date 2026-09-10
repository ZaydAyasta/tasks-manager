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
})
