<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../app/stores/auth.store'
import { apiMessage } from '../../app/services/api'

const auth = useAuthStore()
const router = useRouter()
const currentPassword = ref('')
const newPassword = ref('')
const confirmation = ref('')
const busy = ref(false)
const error = ref('')
const message = ref('')
async function save() {
  error.value = ''; message.value = ''
  if (newPassword.value !== confirmation.value) { error.value = 'Las contraseñas nuevas no coinciden.'; return }
  busy.value = true
  try {
    const wasRequired = auth.user.mustChangePassword
    await auth.changePassword({ currentPassword: currentPassword.value, newPassword: newPassword.value })
    currentPassword.value = ''; newPassword.value = ''; confirmation.value = ''
    message.value = 'Contraseña actualizada. Las demás sesiones han sido cerradas.'
    if (wasRequired) await router.replace(auth.isAdmin ? '/dashboard' : '/my-work')
  } catch (cause) { error.value = apiMessage(cause) }
  finally { busy.value = false }
}
</script>

<template>
  <section class="page account-page">
    <header class="page-header"><div><p class="eyebrow">ACCESO PERSONAL</p><h1>Mi cuenta</h1><p>{{ auth.user?.fullName }} · {{ auth.user?.email }}</p></div></header>
    <section class="detail-section account-form">
      <h2>{{ auth.user?.mustChangePassword ? 'Define tu contraseña para continuar' : 'Cambiar contraseña' }}</h2>
      <p class="muted">{{ auth.user?.mustChangePassword ? 'Tu contraseña es temporal. Elige una propia para entrar al espacio de trabajo.' : 'Al guardar, se cerrarán tus otras sesiones.' }}</p>
      <form class="admin-form" @submit.prevent="save">
        <label>Contraseña actual o temporal<input v-model="currentPassword" type="password" autocomplete="current-password" required /></label>
        <label>Nueva contraseña<input v-model="newPassword" type="password" autocomplete="new-password" minlength="12" maxlength="128" required /></label>
        <label>Confirmar nueva contraseña<input v-model="confirmation" type="password" autocomplete="new-password" minlength="12" maxlength="128" required /></label>
        <p class="muted">Usa de 12 a 128 caracteres y una contraseña distinta de la actual.</p>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <p v-if="message" class="feedback-message" role="status">{{ message }}</p>
        <button class="button primary" :disabled="busy">{{ busy ? 'Guardando...' : 'Guardar contraseña' }}</button>
      </form>
    </section>
  </section>
</template>
