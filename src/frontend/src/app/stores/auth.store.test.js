import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
vi.mock('../services/api', () => ({ api: vi.fn(), configureApi: vi.fn() }))
import { api } from '../services/api'; import { useAuthStore } from './auth.store'
describe('auth store', () => { beforeEach(() => { setActivePinia(createPinia()); localStorage.clear(); vi.clearAllMocks() })
  it('persists the token and user after login', async () => { api.mockResolvedValue({ accessToken: 'jwt', user: { id: '1', role: 'Admin' } }); const store = useAuthStore(); await store.login({ email: 'a@b.c', password: 'secret' }); expect(store.isAuthenticated).toBe(true); expect(localStorage.getItem('nakama.access-token')).toBe('jwt') })
  it('clears the persisted session on logout', async () => { const store = useAuthStore(); localStorage.setItem('nakama.access-token', 'jwt'); store.accessToken = 'jwt'; store.user = { role: 'Admin' }; store.logout(); expect(store.isAuthenticated).toBe(false); expect(localStorage.getItem('nakama.access-token')).toBeNull() })
  it('cleans up a restored session after a 401', async () => { localStorage.setItem('nakama.access-token', 'jwt'); api.mockRejectedValue({ status: 401 }); const store = useAuthStore(); await expect(store.restoreSession()).resolves.toBe(false); expect(store.accessToken).toBeNull(); expect(localStorage.getItem('nakama.access-token')).toBeNull() })
})
