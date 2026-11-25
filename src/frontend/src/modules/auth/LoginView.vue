<script setup>
import { ref, useId } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../app/stores/auth.store'
import logo from '../../shared/assets/nakama-logo.png'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
const email = ref('')
const password = ref('')
const passwordVisible = ref(false)
const emailId = useId()
const passwordId = useId()
const error = ref(route.query.error === 'unavailable' ? 'No se pudo conectar con el servidor. Comprueba que esté disponible e inténtalo de nuevo.' : '')
const submitting = ref(false)

async function submit() {
  error.value = ''
  submitting.value = true

  try {
    const user = await auth.login({ email: email.value, password: password.value })
    const redirect = route.query.redirect
    const safeRedirect = typeof redirect === 'string' && /^\/(?!\/)/.test(redirect) && !redirect.includes('\\')
    const landing = user.role === 'Admin' ? '/dashboard' : '/my-work'
    await router.replace(user.mustChangePassword ? '/account' : safeRedirect ? redirect : landing)
  } catch (cause) {
    error.value = [0, 429].includes(cause?.status) ? cause.message : 'El usuario o la contraseña son incorrectos.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <main class="login-page">
    <section class="workspace-login-card" aria-labelledby="login-title">
      <div class="workspace-login-brand">
        <img :src="logo" alt="Nakama" width="1217" height="303" />
        <div><strong>Gestión de proyectos</strong><p>Consulta tus tareas y comparte el avance con tu equipo.</p></div>
      </div>
      <div class="workspace-login-form">
        <div><h1 id="login-title">Iniciar sesión</h1><p class="muted">Usa el usuario o correo que te dio tu equipo.</p></div>
        <form method="post" @submit.prevent="submit">
          <div class="login-field">
            <label :for="emailId">Usuario o correo</label>
            <input :id="emailId" v-model="email" name="username" type="text" autocomplete="username" autocapitalize="none" spellcheck="false" maxlength="320" required />
          </div>
          <div class="login-field">
            <label :for="passwordId">Contraseña</label>
            <div class="login-password">
              <input :id="passwordId" v-model="password" name="password" :type="passwordVisible ? 'text' : 'password'" autocomplete="current-password" required />
              <button type="button" class="button secondary" :aria-controls="passwordId" :aria-label="passwordVisible ? 'Ocultar contraseña' : 'Mostrar contraseña'" @click="passwordVisible = !passwordVisible">{{ passwordVisible ? 'Ocultar' : 'Mostrar' }}</button>
            </div>
          </div>
          <p v-if="error" class="form-error" role="alert">{{ error }}</p>
          <button type="submit" class="button primary full" :disabled="submitting">{{ submitting ? 'Iniciando sesión...' : 'Iniciar sesión' }}</button>
        </form>
        <p class="recovery-help">Si olvidaste tu contraseña o necesitas una cuenta, contacta a la persona que administra Nakama en tu equipo.</p>
      </div>
    </section>
  </main>
</template>
