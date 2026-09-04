import { ref } from 'vue'
import { defineStore } from 'pinia'
import { notificationsService } from './notifications.service'

export const useNotificationsStore = defineStore('notifications', () => {
  const unreadCount = ref(0)

  async function loadUnreadCount() {
    const response = await notificationsService.unreadCount()
    unreadCount.value = Math.max(0, response?.count ?? 0)
  }

  async function markRead(id) {
    await notificationsService.markRead(id)
    unreadCount.value = Math.max(0, unreadCount.value - 1)
  }

  async function markAllRead() {
    await notificationsService.markAllRead()
    unreadCount.value = 0
  }

  function reset() {
    unreadCount.value = 0
  }

  return { unreadCount, loadUnreadCount, markRead, markAllRead, reset }
})
