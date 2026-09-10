import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, configureApi, resolveApiBaseUrl } from './api'

describe('api client authentication', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    configureApi({ getToken: () => null, getCsrfToken: () => null, onUnauthorized: () => {} })
  })

  it('requires an HTTPS API base URL for production builds', () => {
    expect(() => resolveApiBaseUrl('', true)).toThrow('must be configured')
    expect(() => resolveApiBaseUrl('http://api.nakama.test', true)).toThrow('absolute HTTPS URL')
    expect(() => resolveApiBaseUrl('https://localhost:5000', true)).toThrow('absolute HTTPS URL')
    expect(() => resolveApiBaseUrl('https://user:pass@api.nakama.test', true)).toThrow('without credentials')
    expect(resolveApiBaseUrl('https://api.nakama.test/', true)).toBe('https://api.nakama.test')
  })

  it('includes cookies and the CSRF header for authenticated mutations', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetch)
    configureApi({ getToken: () => null, getCsrfToken: () => 'csrf-value' })

    await api('/api/projects', { method: 'POST', body: { name: 'Pilot' } })

    expect(fetch).toHaveBeenCalledWith('http://localhost:5000/api/projects', expect.objectContaining({
      credentials: 'include',
      headers: expect.objectContaining({ 'X-Nakama-Csrf': 'csrf-value' })
    }))
  })

  it('does not send the CSRF header for safe reads', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify([]), { headers: { 'content-type': 'application/json' } }))
    vi.stubGlobal('fetch', fetch)
    configureApi({ getToken: () => null, getCsrfToken: () => 'csrf-value' })

    await api('/api/projects')

    expect(fetch.mock.calls[0][1].headers['X-Nakama-Csrf']).toBeUndefined()
  })
})
