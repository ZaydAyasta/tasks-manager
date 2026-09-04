import { api } from '../../app/services/api'

export const notificationsService = {
  list({ unreadOnly, cursor } = {}) {
    const query = new URLSearchParams()

    if (unreadOnly) query.set('unreadOnly', 'true')
    if (cursor) query.set('cursor', cursor)

    const suffix = query.size ? `?${query}` : ''
    return api(`/api/me/notifications${suffix}`)
  },

  unreadCount() {
    return api('/api/me/notifications/unread-count')
  },

  markRead(id) {
    return api(`/api/me/notifications/${id}/read`, { method: 'POST' })
  },

  markAllRead() {
    return api('/api/me/notifications/read-all', { method: 'POST' })
  },

  getPreferences() {
    return api('/api/me/notification-preferences')
  },

  updatePreference(type, enabled) {
    return api(`/api/me/notification-preferences/${type}`, {
      method: 'PUT',
      body: { enabled }
    })
  }
}
