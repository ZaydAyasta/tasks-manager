<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../app/stores/auth.store'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
const email = ref('')
const password = ref('')
const error = ref(route.query.error === 'unavailable' ? 'No se pudo conectar con el servidor. Comprueba que esté disponible e inténtalo de nuevo.' : '')
const submitting = ref(false)

async function submit() {
  error.value = ''
  submitting.value = true

  try {
    const user = await auth.login({ email: email.value, password: password.value })
    router.replace(user.mustChangePassword ? '/account' : route.query.redirect || (user.role === 'Admin' ? '/dashboard' : '/my-work'))
  } catch (cause) {
    error.value = [0, 429].includes(cause?.status) ? cause.message : 'Credenciales inválidas.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <section class="login-panel">
      <div class="brand standalone"><span>N</span><b>Nakama</b></div>
      <p class="eyebrow">GESTIÓN DE TRABAJO</p>
      <h1>Entra a tu espacio de trabajo.</h1>
      <p class="login-copy">Coordina proyectos, decisiones y tareas desde un único lugar.</p>
      <form @submit.prevent="submit">
        <label>Usuario o correo<input v-model="email" type="text" autocomplete="username" maxlength="320" required /></label>
        <label>Contraseña<input v-model="password" type="password" autocomplete="current-password" required /></label>
        <p v-if="error" class="form-error" role="alert">{{ error }}</p>
        <button class="button primary full" :disabled="submitting">{{ submitting ? 'Ingresando…' : 'Ingresar' }}</button>
      </form>
      <p class="recovery-help">Si olvidaste tu contraseña, solicita a un administrador que la restablezca. No necesitas un correo real.</p>
    </section>
  </main>
</template>
