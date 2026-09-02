import { api } from '../../app/services/api'
export const stagesService = {
  create: (projectId, body) => api(`/api/projects/${projectId}/stages`, { method: 'POST', body }), update: (projectId, id, body) => api(`/api/projects/${projectId}/stages/${id}`, { method: 'PUT', body }),
  toggle: (projectId, id, action, version) => api(`/api/projects/${projectId}/stages/${id}/${action}`, { method: 'POST', body: { version } }), reorder: (projectId, stages) => api(`/api/projects/${projectId}/stages/order`, { method: 'PUT', body: { stages } }),
  members: (projectId, id) => api(`/api/projects/${projectId}/stages/${id}/members`), addMember: (projectId, id, userId, role) => api(`/api/projects/${projectId}/stages/${id}/members`, { method: 'POST', body: { userId, role } }), removeMember: (projectId, id, userId) => api(`/api/projects/${projectId}/stages/${id}/members/${userId}`, { method: 'DELETE' })
}
