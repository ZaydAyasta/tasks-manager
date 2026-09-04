import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppLayout from './AppLayout.vue'

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))
vi.mock('../../modules/notifications/notifications.service', () => ({
  notificationsService: { unreadCount: vi.fn().mockResolvedValue({ count: 0 }) }
}))

import { useNotificationsStore } from '../../modules/notifications/notifications.store'
import { useAuthStore } from '../stores/auth.store'

function mountLayout(user = null) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const auth = useAuthStore()
  auth.user = user
  auth.accessToken = user ? 'token' : null

  return mount(AppLayout, {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: { template: '<a><slot /></a>' }, RouterView: true }
    }
  })
}

describe('AppLayout notification badge', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('hides the badge when the unread count is zero', async () => {
    const wrapper = mountLayout()
    useNotificationsStore().unreadCount = 0
    await flushPromises()

    expect(wrapper.find('.nav-notification-badge').exists()).toBe(false)
  })

  it('shows the count and caps it at 9+', async () => {
    const wrapper = mountLayout()
    useNotificationsStore().unreadCount = 7
    await flushPromises()
    expect(wrapper.get('.nav-notification-badge').text()).toBe('7')

    useNotificationsStore().unreadCount = 10
    await flushPromises()
    expect(wrapper.get('.nav-notification-badge').text()).toBe('9+')
  })

  it('shows Dashboard only to Admin users', async () => {
    const collaborator = mountLayout({ fullName: 'Collaborator', role: 'Collaborator' })
    await flushPromises()
    expect(collaborator.text()).not.toContain('Dashboard')

    const admin = mountLayout({ fullName: 'Admin', role: 'Admin' })
    await flushPromises()
    expect(admin.text()).toContain('Dashboard')
  })
})
