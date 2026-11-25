import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { api, configureApi } from '../services/api'

export const useAuthStore = defineStore('auth', () => {
  const user = ref(null)
  const accessToken = ref(null)
  const csrfToken = ref(null)
  const isLoading = ref(false)
  const sessionChecked = ref(false)

  const isAuthenticated = computed(() => Boolean(user.value))
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isCollaborator = computed(() => user.value?.role === 'Collaborator')

  const clear = () => {
    accessToken.value = null
    csrfToken.value = null
    user.value = null
    sessionChecked.value = true
  }

  const setSession = (token, csrf, currentUser) => {
    accessToken.value = token || null
    csrfToken.value = csrf
    user.value = currentUser
    sessionChecked.value = true
  }

  async function login(credentials) {
    isLoading.value = true

    try {
      const signIn = () => api('/api/auth/login', { method: 'POST', body: credentials })
      let result
      try {
        result = await signIn()
      } catch (error) {
        if (error.problem?.type !== 'https://nakama/errors/csrf-invalid') throw error
        const current = await api('/api/auth/me')
        csrfToken.value = current.csrfToken
        result = await signIn()
      }
      setSession(result.accessToken, result.csrfToken, result.user)
      return result.user
    } finally {
      isLoading.value = false
    }
  }

  async function loadCurrentUser() {
    const current = await api('/api/auth/me')
    const { csrfToken: currentCsrfToken, ...currentUser } = current

    setSession(accessToken.value, currentCsrfToken, currentUser)
    return currentUser
  }

  async function changePassword(credentials) {
    const result = await api('/api/auth/change-password', { method: 'POST', body: credentials })
    setSession(result.accessToken, result.csrfToken, result.user)
    return result.user
  }

  async function restoreSession() {
    if (sessionChecked.value) return isAuthenticated.value

    isLoading.value = true

    try {
      await loadCurrentUser()
      return true
    } catch (error) {
      if (error.status === 401) {
        clear()
        return false
      }

      throw error
    } finally {
      sessionChecked.value = true
      isLoading.value = false
    }
  }

  async function logout() {
    if (isAuthenticated.value) {
      try {
        await api('/api/auth/logout', { method: 'POST' })
      } catch (error) {
        if (error.status !== 401) throw error
      }
    }
    clear()
  }

  configureApi({ getToken: () => accessToken.value, getCsrfToken: () => csrfToken.value, onUnauthorized: clear })

  return {
    user,
    accessToken,
    csrfToken,
    isLoading,
    sessionChecked,
    isAuthenticated,
    isAdmin,
    isCollaborator,
    login,
    changePassword,
    logout,
    loadCurrentUser,
    restoreSession,
    clear
  }
})
