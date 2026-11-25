<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useNotificationsStore } from '../../modules/notifications/notifications.store'
import { useAuthStore } from '../stores/auth.store'
import BaseModal from '../../shared/components/BaseModal.vue'
import WorkspaceNavigation from './WorkspaceNavigation.vue'
import logo from '../../shared/assets/nakama-logo.png'
import { roleLabel } from '../../shared/presentation'

const auth = useAuthStore()
const notificationsStore = useNotificationsStore()
const router = useRouter()
const route = useRoute()
const menuOpen = ref(false)
const logoutPending = ref(false)
const logoutError = ref('')
const account = { to: '/account', label: 'Mi cuenta', description: 'Cambia tu contraseña' }

const navigation = computed(() => {
  if (auth.user?.mustChangePassword) return [account]

  const work = { to: '/my-work', label: 'Mi trabajo', description: 'Abre tus tareas asignadas' }
  const projects = { to: '/projects', label: auth.isAdmin ? 'Proyectos' : 'Mis proyectos', description: 'Revisa el tablero y el equipo' }
  const primary = auth.isAdmin
    ? [
        { to: '/dashboard', label: 'Resumen', description: 'Revisa el avance y los bloqueos' },
        projects, work,
        { to: '/users', label: 'Usuarios', description: 'Administra las cuentas' }
      ]
    : [work, projects]

  return [...primary, { to: '/notifications', label: 'Notificaciones', description: 'Consulta las novedades' }, account]
})

const home = computed(() => auth.user?.mustChangePassword ? '/account' : auth.isAdmin ? '/dashboard' : '/my-work')
const activeSection = computed(() => route.path?.startsWith('/projects/') || route.path?.startsWith('/tasks/') ? '/projects' : undefined)
let compactViewport
function closeMenuOnDesktop({ matches }) {
  if (!matches) menuOpen.value = false
}

async function logout() {
  logoutPending.value = true
  logoutError.value = ''
  try {
    await auth.logout()
    notificationsStore.reset()
    await router.push('/login')
  } catch {
    logoutError.value = 'No se pudo cerrar la sesión. Inténtalo de nuevo.'
  } finally {
    logoutPending.value = false
  }
}

onMounted(() => {
  compactViewport = window.matchMedia?.('(max-width: 960px)')
  compactViewport?.addEventListener('change', closeMenuOnDesktop)
  if (auth.isAuthenticated && !auth.user?.mustChangePassword) notificationsStore.loadUnreadCount().catch(() => {})
})
onBeforeUnmount(() => compactViewport?.removeEventListener('change', closeMenuOnDesktop))
</script>

<template>
  <div class="app-shell workspace-shell">
    <a class="skip-link" href="#contenido">Saltar al contenido</a>
    <aside class="sidebar workspace-sidebar">
      <RouterLink :to="home" class="workspace-brand"><img :src="logo" alt="Nakama" width="1217" height="303" /><span>Gestión de proyectos</span></RouterLink>
      <WorkspaceNavigation :items="navigation" :unread-count="notificationsStore.unreadCount" :active-section="activeSection" />
    </aside>
    <div class="main-content">
      <header class="workspace-topbar">
        <div class="workspace-topbar-start">
          <button type="button" class="button secondary mobile-menu-button" aria-haspopup="dialog" aria-controls="workspace-menu" @click="menuOpen = true">Menú</button>
          <div class="workspace-location"><small>Nakama · Proyectos</small><strong>{{ route.meta.title || 'Espacio de trabajo' }}</strong></div>
        </div>
        <div class="workspace-session">
          <div v-if="auth.user" class="workspace-person"><strong>{{ auth.user.fullName }}</strong><small>{{ roleLabel(auth.user.role) }}</small></div>
          <button type="button" class="button secondary" :disabled="logoutPending" @click="logout">Cerrar sesión</button>
        </div>
      </header>
      <main id="contenido" tabindex="-1">
        <p v-if="logoutError" class="workspace-alert form-error" role="alert">{{ logoutError }}</p>
        <RouterView :key="route.path" />
      </main>
    </div>
    <BaseModal id="workspace-menu" v-model="menuOpen" title="Menú de navegación" class="workspace-drawer">
      <WorkspaceNavigation v-if="menuOpen" :items="navigation" :unread-count="notificationsStore.unreadCount" :active-section="activeSection" @navigate="menuOpen = false" />
    </BaseModal>
  </div>
</template>
