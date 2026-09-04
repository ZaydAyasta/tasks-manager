<script setup>
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useNotificationsStore } from '../../modules/notifications/notifications.store'
import { useAuthStore } from '../stores/auth.store'

const auth = useAuthStore()
const notificationsStore = useNotificationsStore()
const router = useRouter()

const navigation = computed(() => {
  const primary = auth.isAdmin
    ? [{ to: '/dashboard', label: 'Dashboard' }, { to: '/projects', label: 'Proyectos' }, { to: '/my-work', label: 'Mi trabajo' }]
    : [{ to: '/my-work', label: 'Mi trabajo' }, { to: '/projects', label: 'Mis proyectos' }]

  return [...primary, { to: '/notifications', label: 'Notificaciones', notificationAction: true }]
})

const home = computed(() => auth.isAdmin ? '/dashboard' : '/my-work')

function notificationBadge(count) {
  if (count <= 0) return ''
  return count > 9 ? '9+' : String(count)
}

function logout() {
  auth.logout()
  notificationsStore.reset()
  router.push('/login')
}

onMounted(() => {
  if (auth.isAuthenticated) notificationsStore.loadUnreadCount().catch(() => {})
})
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar">
      <RouterLink :to="home" class="brand"><span>N</span><b>Nakama</b></RouterLink>
      <nav>
        <RouterLink v-for="item in navigation" :key="item.to" :to="item.to" class="nav-link">
          <svg v-if="item.notificationAction" class="notification-bell" viewBox="0 0 24 24" aria-hidden="true">
            <path d="M18 9a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4" />
          </svg>
          <span>{{ item.label }}</span>
          <span v-if="item.notificationAction && notificationBadge(notificationsStore.unreadCount)" class="nav-notification-badge">
            {{ notificationBadge(notificationsStore.unreadCount) }}
          </span>
        </RouterLink>
      </nav>
      <div class="sidebar-footer">
        <div class="user-label"><b>{{ auth.user?.fullName }}</b><small>{{ auth.user?.role }}</small></div>
        <button class="text-button" @click="logout">Cerrar sesión</button>
      </div>
    </aside>
    <main class="main-content"><RouterView /></main>
  </div>
</template>
