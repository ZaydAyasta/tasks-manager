import { api } from '../../app/services/api'
export const workflowActions = (status, isAdmin) => {
  if (status === 'Pending') return [{ id: 'start', label: 'Iniciar' }]
  if (status === 'InProgress') return [{ id: 'submit-review', label: 'Enviar a revisión' }]
  if (status === 'InReview' && isAdmin) return [{ id: 'request-changes', label: 'Solicitar cambios', secondary: true }, { id: 'complete', label: 'Completar' }]
  return []
}
export const tasksService = {
  get: id => api(`/api/tasks/${id}`), subtasks: id => api(`/api/tasks/${id}/subtasks`), blockers: id => api(`/api/tasks/${id}/blockers`), dependencies: id => api(`/api/tasks/${id}/dependencies`), dependents: id => api(`/api/tasks/${id}/dependents`), activity: id => api(`/api/tasks/${id}/activity`),
  workflow: (id, action, version) => api(`/api/tasks/${id}/${action}`, { method: 'POST', body: { version } }),
  createSubtask: (id, title, taskVersion) => api(`/api/tasks/${id}/subtasks`, { method: 'POST', body: { title, taskVersion } }),
  subtaskAction: (taskId, id, action, taskVersion) => api(`/api/tasks/${taskId}/subtasks/${id}/${action}`, { method: 'POST', body: { taskVersion } }),
  deleteSubtask: (taskId, id, taskVersion) => api(`/api/tasks/${taskId}/subtasks/${id}`, { method: 'DELETE', body: { taskVersion } }),
  reportBlocker: (id, type, description, version) => api(`/api/tasks/${id}/blockers`, { method: 'POST', body: { type, description, version } }),
  resolveBlocker: (taskId, id, version) => api(`/api/tasks/${taskId}/blockers/${id}/resolve`, { method: 'POST', body: { version } }),
  create: (projectId, body) => api(`/api/projects/${projectId}/tasks`, { method: 'POST', body }), update: (id, body) => api(`/api/tasks/${id}`, { method: 'PUT', body }), move: (id, stageId, version) => api(`/api/tasks/${id}/stage`, { method: 'PUT', body: { stageId, version } }), addAssignee: (id, userId, version) => api(`/api/tasks/${id}/assignees`, { method: 'POST', body: { userId, version } }), removeAssignee: (id, userId, version) => api(`/api/tasks/${id}/assignees/${userId}`, { method: 'DELETE', body: { version } })
}
