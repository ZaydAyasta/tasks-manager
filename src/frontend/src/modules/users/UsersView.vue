<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useAuthStore } from '../../app/stores/auth.store'
import { apiMessage } from '../../app/services/api'
import BaseModal from '../../shared/components/BaseModal.vue'
import { userAdminService } from './users.service'

const auth = useAuthStore()
const users = ref([])
const search = ref('')
const state = ref('all')
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const message = ref('')
const createOpen = ref(false)
const resetUser = ref(null)
const temporaryPassword = ref('')
const form = ref({ fullName: '', email: '', role: 'Collaborator', password: '' })
watch(createOpen, open => { if (!open) form.value = { fullName: '', email: '', role: 'Collaborator', password: '' } })
const visibleUsers = computed(() => users.value.filter(user =>
  `${user.fullName} ${user.email}`.toLowerCase().includes(search.value.toLowerCase()) &&
  (state.value === 'all' || user.isActive === (state.value === 'active'))))

async function load() {
  loading.value = true
  try { users.value = await userAdminService.list() } catch (cause) { error.value = apiMessage(cause) }
  finally { loading.value = false }
}
async function mutate(operation, success) {
  if (busy.value) return
  busy.value = true
  error.value = ''; message.value = ''
  try { await operation(); message.value = success; await load() }
  catch (cause) { error.value = apiMessage(cause) }
  finally { busy.value = false }
}
async function create() {
  await mutate(async () => {
    await userAdminService.create({ ...form.value, mustChangePassword: true })
    form.value = { fullName: '', email: '', role: 'Collaborator', password: '' }
    createOpen.value = false
  }, 'Cuenta creada. Comunica la contraseña temporal a la persona por un canal privado; deberá cambiarla al entrar.')
}
async function reset() {
  await mutate(async () => {
    await userAdminService.resetPassword(resetUser.value.id, temporaryPassword.value)
    temporaryPassword.value = ''; resetUser.value = null
  }, 'Contraseña restablecida y sesiones anteriores revocadas. La persona deberá cambiar la contraseña temporal al entrar.')
}
function openReset(user) { temporaryPassword.value = ''; resetUser.value = user }
function toggle(user) {
  if (!user.isActive || window.confirm(`¿Desactivar a ${user.fullName}? Perderá el acceso al sistema.`))
    mutate(() => userAdminService.setActive(user.id, !user.isActive), 'Estado de la cuenta actualizado.')
}
function changeRole(user, event) {
  const role = event.target.value
  event.target.value = user.role
  if (role !== user.role && window.confirm(`¿Cambiar el rol de ${user.fullName} a ${role === 'Admin' ? 'administrador' : 'colaborador'}?`))
    mutate(() => userAdminService.changeRole(user.id, role), 'Rol actualizado.')
}
onMounted(load)
</script>

<template>
  <section class="page users-page">
    <header class="page-header"><div><p class="eyebrow">EQUIPO Y ACCESO</p><h1>Usuarios</h1><p>Gestiona quién entra a Nakama y qué puede administrar.</p></div><button class="button primary" @click="createOpen = true">Crear usuario</button></header>
    <p v-if="error" class="form-error" role="alert">{{ error }}</p>
    <p v-if="message" class="feedback-message" role="status">{{ message }}</p>
    <form class="filter-bar" @submit.prevent>
      <label>Buscar persona<input v-model="search" type="search" placeholder="Nombre o usuario" /></label>
      <label>Estado<select v-model="state"><option value="all">Todas las cuentas</option><option value="active">Activas</option><option value="inactive">Inactivas</option></select></label>
    </form>
    <p v-if="loading" class="state-card">Cargando usuarios...</p>
    <div v-else-if="visibleUsers.length" class="table-scroll">
      <table class="team-table"><thead><tr><th>Persona</th><th>Acceso</th><th>Rol</th><th>Acciones</th></tr></thead><tbody>
        <tr v-for="user in visibleUsers" :key="user.id">
          <td><b>{{ user.fullName }}</b><small>{{ user.email }}</small><small v-if="user.mustChangePassword">Cambio de contraseña pendiente</small></td>
          <td><span class="badge" :class="user.isActive ? 'active' : 'paused'">{{ user.isActive ? 'Activo' : 'Inactivo' }}</span></td>
          <td><select :aria-label="`Rol de ${user.fullName}`" :value="user.role" :disabled="busy || user.id === auth.user.id" @change="changeRole(user, $event)"><option value="Collaborator">Colaborador</option><option value="Admin">Administrador</option></select></td>
          <td><div v-if="user.id !== auth.user.id" class="action-row"><button class="text-button" :disabled="busy" @click="openReset(user)">Restablecer contraseña</button><button class="text-button" :class="{ danger: user.isActive }" :disabled="busy" @click="toggle(user)">{{ user.isActive ? 'Desactivar' : 'Activar' }}</button></div><span v-else class="muted">Tu cuenta</span></td>
        </tr>
      </tbody></table>
    </div>
    <p v-else class="state-card">No hay personas que coincidan con los filtros.</p>
    <BaseModal v-model="createOpen" title="Crear usuario">
      <form class="admin-form" @submit.prevent="create">
        <label>Nombre completo<input v-model="form.fullName" maxlength="150" required /></label>
        <label>Usuario o correo<input v-model="form.email" autocomplete="off" maxlength="320" required /></label>
        <p class="muted">Un usuario como ana se guardará como ana@trello.local. No se envían correos.</p>
        <label>Rol<select v-model="form.role"><option value="Collaborator">Colaborador</option><option value="Admin">Administrador</option></select></label>
        <label>Contraseña temporal<input v-model="form.password" type="password" autocomplete="new-password" minlength="12" maxlength="128" required /></label>
        <p class="muted">De 12 a 128 caracteres. La persona deberá cambiarla al entrar.</p>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <button class="button primary" :disabled="busy">{{ busy ? 'Creando...' : 'Crear cuenta' }}</button>
      </form>
    </BaseModal>
    <BaseModal :model-value="Boolean(resetUser)" title="Restablecer acceso" @update:model-value="value => { if (!value) { resetUser = null; temporaryPassword = '' } }">
      <form v-if="resetUser" class="admin-form" @submit.prevent="reset">
        <p>Se cerrarán las sesiones de <b>{{ resetUser.fullName }}</b>. Comunica la contraseña temporal de forma privada.</p>
        <label>Nueva contraseña temporal<input v-model="temporaryPassword" type="password" autocomplete="new-password" minlength="12" maxlength="128" required /></label>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <button class="button primary" :disabled="busy">{{ busy ? 'Restableciendo...' : 'Restablecer acceso' }}</button>
      </form>
    </BaseModal>
  </section>
</template>
