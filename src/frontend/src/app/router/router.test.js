import { describe, expect, it } from 'vitest'
import { resolveNavigation, restoreSessionOrRedirect, routes } from './index'
describe('router configuration', () => {
  it('marks the project route as protected', () => expect(routes[1].children.find(route => route.path === 'projects').meta.requiresAuth).toBe(true))
  it('redirects an anonymous user to login for a protected route', () => expect(resolveNavigation({ meta: { requiresAuth: true }, fullPath: '/projects' }, { isAuthenticated: false })).toEqual({ path: '/login', query: { redirect: '/projects' } }))
  it('sends a user without the required role to forbidden', () => expect(resolveNavigation({ meta: { roles: ['Admin'] } }, { isAuthenticated: true, user: { role: 'Collaborator' } })).toBe('/403'))
  it('routes Admin users to dashboard and collaborators to My Work by default', () => {
    expect(resolveNavigation({ path: '/', meta: { requiresAuth: true } }, { isAuthenticated: true, isAdmin: true })).toBe('/dashboard')
    expect(resolveNavigation({ path: '/', meta: { requiresAuth: true } }, { isAuthenticated: true, isAdmin: false })).toBe('/my-work')
    expect(resolveNavigation({ meta: { guest: true } }, { isAuthenticated: true, isAdmin: true })).toBe('/dashboard')
    expect(resolveNavigation({ meta: { guest: true } }, { isAuthenticated: true, isAdmin: false })).toBe('/my-work')
  })
  it('declares Dashboard as an Admin-only route', () => {
    const dashboard = routes[1].children.find(route => route.path === 'dashboard')
    expect(dashboard.meta.roles).toEqual(['Admin'])
  })
  it('redirects to login when session restoration cannot reach the API', async () => {
    const auth = { sessionChecked: false, isAuthenticated: false, restoreSession: () => Promise.reject(new Error('Network unavailable')) }

    await expect(restoreSessionOrRedirect({ meta: { requiresAuth: true }, fullPath: '/projects' }, auth))
      .resolves.toEqual({ path: '/login', query: { redirect: '/projects', error: 'unavailable' } })
  })
  it('does not redirect recursively when session restoration fails on login', async () => {
    const auth = { sessionChecked: false, isAuthenticated: false, restoreSession: () => Promise.reject(new Error('Network unavailable')) }

    await expect(restoreSessionOrRedirect({ path: '/login', meta: { guest: true }, fullPath: '/login' }, auth)).resolves.toBeUndefined()
  })
})
