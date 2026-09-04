import { describe, expect, it, vi } from 'vitest'

vi.mock('../../app/services/api', () => ({ api: vi.fn() }))

import { api } from '../../app/services/api'
import { notificationsService } from './notifications.service'

describe('notifications service', () => {
  it('uses the notifications feed endpoint with its filters', async () => {
    await notificationsService.list({ unreadOnly: true, cursor: 'next-page' })

    expect(api).toHaveBeenCalledWith('/api/me/notifications?unreadOnly=true&cursor=next-page')
  })

  it('uses the preferences endpoints', async () => {
    await notificationsService.getPreferences()
    await notificationsService.updatePreference('TaskAssigned', false)

    expect(api).toHaveBeenCalledWith('/api/me/notification-preferences')
    expect(api).toHaveBeenCalledWith('/api/me/notification-preferences/TaskAssigned', {
      method: 'PUT',
      body: { enabled: false }
    })
  })
})
