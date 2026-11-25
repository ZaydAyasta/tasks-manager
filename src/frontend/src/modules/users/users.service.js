import { api } from '../../app/services/api'

export const userAdminService = {
  list: () => api('/api/users'),
  create: body => api('/api/users', { method: 'POST', body }),
  setActive: (id, active) => api(`/api/users/${id}/${active ? 'activate' : 'deactivate'}`, { method: 'POST' }),
  changeRole: (id, role) => api(`/api/users/${id}/role`, { method: 'PUT', body: { role } }),
  resetPassword: (id, temporaryPassword) => api(`/api/users/${id}/reset-password`, { method: 'POST', body: { temporaryPassword } })
}
