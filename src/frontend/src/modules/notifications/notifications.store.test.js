import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('./notifications.service', () => ({
  notificationsService: {
    unreadCount: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn()
  }
}))

import { notificationsService } from './notifications.service'
import { useNotificationsStore } from './notifications.store'

describe('notifications store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('does not allow the unread count to become negative', async () => {
    notificationsService.markRead.mockResolvedValue(null)
    const store = useNotificationsStore()

    await store.markRead('notification-1')

    expect(store.unreadCount).toBe(0)
  })

  it('resets the count during logout coordination', () => {
    const store = useNotificationsStore()
    store.unreadCount = 4

    store.reset()

    expect(store.unreadCount).toBe(0)
  })
})
