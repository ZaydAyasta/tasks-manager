<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { apiMessage } from '../../app/services/api'
import { notificationPreferenceLabels, presentNotification } from './notification-presenter'
import { notificationsService } from './notifications.service'
import { useNotificationsStore } from './notifications.store'

const router = useRouter()
const notificationsStore = useNotificationsStore()
const items = ref([])
const preferences = ref([])
const unreadOnly = ref(false)
const isLoading = ref(true)
const isMarkingAllRead = ref(false)
const error = ref('')
const changingPreference = ref('')
const emptyMessage = computed(() => unreadOnly.value ? 'Estás al día.' : 'No tienes notificaciones.')

function formatDate(value) {
  if (!value) return 'Fecha no disponible'

  return new Intl.DateTimeFormat('es-PE', {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(new Date(value))
}

async function load() {
  isLoading.value = true
  error.value = ''

  try {
    const [feed, preferenceItems] = await Promise.all([
      notificationsService.list({ unreadOnly: unreadOnly.value }),
      notificationsService.getPreferences()
    ])
    items.value = feed?.items ?? []
    preferences.value = preferenceItems ?? []
  } catch (requestError) {
    error.value = apiMessage(requestError)
  } finally {
    isLoading.value = false
  }
}

async function selectFilter(value) {
  unreadOnly.value = value
  await load()
}

async function markAllRead() {
  if (isMarkingAllRead.value) return

  isMarkingAllRead.value = true
  error.value = ''
  try {
    await notificationsStore.markAllRead()
    await load()
  } catch (requestError) {
    error.value = apiMessage(requestError)
  } finally {
    isMarkingAllRead.value = false
  }
}

async function openNotification(notification) {
  error.value = ''
  try {
    if (!notification.isRead) {
      await notificationsStore.markRead(notification.id)
      notification.isRead = true
    }

    if (notification.taskId) await router.push(`/tasks/${notification.taskId}`)
    else if (notification.projectId) await router.push(`/projects/${notification.projectId}`)
  } catch (requestError) {
    error.value = apiMessage(requestError)
  }
}

async function changePreference(preference, event) {
  const previousValue = preference.enabled
  const enabled = event.target.checked
  preference.enabled = enabled
  changingPreference.value = preference.type
  error.value = ''

  try {
    await notificationsService.updatePreference(preference.type, enabled)
  } catch (requestError) {
    preference.enabled = previousValue
    error.value = apiMessage(requestError)
  } finally {
    changingPreference.value = ''
  }
}

onMounted(async () => {
  await Promise.all([load(), notificationsStore.loadUnreadCount()])
})
</script>

<template>
  <section class="page notifications-page">
    <header class="page-header">
      <div>
        <p class="eyebrow">ACTIVIDAD PERSONAL</p>
        <h1>Notificaciones</h1>
        <p>Actualizaciones relevantes para tu trabajo.</p>
      </div>
      <button class="button secondary" :disabled="isMarkingAllRead || !items.some(item => !item.isRead)" @click="markAllRead">
        Marcar todas como leídas
      </button>
    </header>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>

    <div class="notifications-layout">
      <section class="notifications-feed" aria-label="Centro de notificaciones">
        <div class="local-tabs" role="tablist" aria-label="Filtro de notificaciones">
          <button :class="{ active: !unreadOnly }" role="tab" :aria-selected="!unreadOnly" @click="selectFilter(false)">Todas</button>
          <button :class="{ active: unreadOnly }" role="tab" :aria-selected="unreadOnly" @click="selectFilter(true)">No leídas</button>
        </div>

        <p v-if="isLoading" class="state-card">Cargando notificaciones...</p>
        <p v-else-if="!items.length" class="state-card">{{ emptyMessage }}</p>

        <div v-else class="notification-list">
          <button v-for="notification in items" :key="notification.id" class="notification-item" :class="{ 'notification-unread': !notification.isRead }" @click="openNotification(notification)">
            <span class="notification-status" :aria-label="notification.isRead ? 'Leída' : 'No leída'"></span>
            <span class="notification-content">
              <strong>{{ presentNotification(notification) }}</strong>
              <span class="notification-meta">{{ notification.actor?.fullName || 'Sistema' }} · {{ formatDate(notification.createdAt) }}</span>
            </span>
            <span class="notification-state">{{ notification.isRead ? 'Leída' : 'Nueva' }}</span>
          </button>
        </div>
      </section>

      <aside class="notification-preferences detail-section">
        <header><div><h2>Preferencias</h2><p>Elige qué actividad quieres recibir en la aplicación.</p></div></header>
        <div class="preference-list">
          <label v-for="preference in preferences" :key="preference.type" class="preference-row">
            <span>{{ notificationPreferenceLabels[preference.type] || preference.type }}</span>
            <input type="checkbox" :checked="preference.enabled" :disabled="changingPreference === preference.type" @change="changePreference(preference, $event)">
          </label>
        </div>
      </aside>
    </div>
  </section>
</template>
