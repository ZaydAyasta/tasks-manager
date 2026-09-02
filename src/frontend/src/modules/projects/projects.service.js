import { api } from '../../app/services/api'
export const projectsService = {
  list: () => api('/api/projects'), get: id => api(`/api/projects/${id}`), stages: id => api(`/api/projects/${id}/stages`),
  tasks: (id, query = {}) => api(`/api/projects/${id}/tasks${Object.keys(query).length ? `?${new URLSearchParams(query)}` : ''}`), userProjects: id => api(`/api/users/${id}/projects`),
  create: body => api('/api/projects', { method: 'POST', body }), update: (id, body) => api(`/api/projects/${id}`, { method: 'PUT', body }), lifecycle: (id, action, version) => api(`/api/projects/${id}/${action}`, { method: 'POST', body: { version } }),
  members: id => api(`/api/projects/${id}/members`), addMember: (id, userId) => api(`/api/projects/${id}/members`, { method: 'POST', body: { userId } }), removeMember: (id, userId) => api(`/api/projects/${id}/members/${userId}`, { method: 'DELETE' })
}
