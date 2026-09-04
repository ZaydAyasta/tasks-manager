import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import NotificationsView from './NotificationsView.vue'

const push = vi.fn()

vi.mock('vue-router', () => ({ useRouter: () => ({ push }) }))
vi.mock('./notifications.service', () => ({
  notificationsService: {
    list: vi.fn(),
    unreadCount: vi.fn(),
    markRead: vi.fn(),
    markAllRead: vi.fn(),
    getPreferences: vi.fn(),
    updatePreference: vi.fn()
  }
}))

import { notificationsService } from './notifications.service'
import { useNotificationsStore } from './notifications.store'

const item = {
  id: 'notification-1',
  type: 'TaskAssigned',
  isRead: false,
  createdAt: '2026-09-02T10:00:00Z',
  actor: { fullName: 'Ana Pérez' },
  taskId: 'task-1',
  metadata: { taskTitle: 'Preparar informe' }
}

function mountView() {
  const pinia = createPinia()
  setActivePinia(pinia)

  return mount(NotificationsView, { global: { plugins: [pinia] } })
}

describe('NotificationsView', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    item.isRead = false
    notificationsService.list.mockResolvedValue({ items: [item] })
    notificationsService.getPreferences.mockResolvedValue([{ type: 'TaskAssigned', enabled: true }])
    notificationsService.unreadCount.mockResolvedValue({ count: 2 })
    notificationsService.markRead.mockResolvedValue(null)
    notificationsService.markAllRead.mockResolvedValue(null)
    notificationsService.updatePreference.mockResolvedValue(null)
  })

  it('shows notifications and distinguishes unread items', async () => {
    const wrapper = mountView()
    await flushPromises()

    expect(wrapper.text()).toContain('Ana Pérez te asignó “Preparar informe”')
    expect(wrapper.find('.notification-unread').exists()).toBe(true)
  })

  it('requests the unread filter', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.get('button[role="tab"][aria-selected="false"]').trigger('click')
    await flushPromises()

    expect(notificationsService.list).toHaveBeenLastCalledWith({ unreadOnly: true })
  })

  it('shows an empty state for each filter', async () => {
    notificationsService.list.mockResolvedValue({ items: [] })
    const wrapper = mountView()
    await flushPromises()
    expect(wrapper.text()).toContain('No tienes notificaciones.')

    await wrapper.get('button[role="tab"][aria-selected="false"]').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Estás al día.')
  })

  it('marks an unread notification and navigates to its task', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.get('.notification-item').trigger('click')
    await flushPromises()

    expect(notificationsService.markRead).toHaveBeenCalledWith('notification-1')
    expect(push).toHaveBeenCalledWith('/tasks/task-1')
  })

  it('marks all as read and clears the global count', async () => {
    const wrapper = mountView()
    await flushPromises()
    useNotificationsStore().unreadCount = 2

    await wrapper.get('.page-header .button').trigger('click')
    await flushPromises()

    expect(notificationsService.markAllRead).toHaveBeenCalledOnce()
    expect(useNotificationsStore().unreadCount).toBe(0)
  })

  it('updates a notification preference', async () => {
    const wrapper = mountView()
    await flushPromises()

    await wrapper.get('.preference-row input').setValue(false)

    expect(notificationsService.updatePreference).toHaveBeenCalledWith('TaskAssigned', false)
  })
})
