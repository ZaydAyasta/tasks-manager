import { describe, expect, it } from 'vitest'
import { resolveNavigation, routes } from './index'
describe('router configuration', () => {
  it('marks the project route as protected', () => expect(routes[1].children.find(route => route.path === 'projects').meta.requiresAuth).toBe(true))
  it('redirects an anonymous user to login for a protected route', () => expect(resolveNavigation({ meta: { requiresAuth: true }, fullPath: '/projects' }, { isAuthenticated: false })).toEqual({ path: '/login', query: { redirect: '/projects' } }))
  it('sends a user without the required role to forbidden', () => expect(resolveNavigation({ meta: { roles: ['Admin'] } }, { isAuthenticated: true, user: { role: 'Collaborator' } })).toBe('/403'))
})
