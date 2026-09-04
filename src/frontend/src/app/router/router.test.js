import { describe, expect, it } from 'vitest'
import { resolveNavigation, routes } from './index'
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
})
