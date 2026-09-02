import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { api, configureApi } from '../services/api'

const TOKEN_KEY = 'nakama.access-token' // MVP temporal: JWT en localStorage hasta que el backend soporte cookies HttpOnly/refresh.

export const useAuthStore = defineStore('auth', () => {
  const user = ref(null); const accessToken = ref(null); const isLoading = ref(false)
  const isAuthenticated = computed(() => Boolean(accessToken.value && user.value))
  const isAdmin = computed(() => user.value?.role === 'Admin')
  const isCollaborator = computed(() => user.value?.role === 'Collaborator')
  const clear = () => { accessToken.value = null; user.value = null; localStorage.removeItem(TOKEN_KEY) }
  const logout = () => clear()
  const setSession = (token, currentUser) => { accessToken.value = token; user.value = currentUser; localStorage.setItem(TOKEN_KEY, token) }
  async function login(credentials) { isLoading.value = true; try { const result = await api('/api/auth/login', { method: 'POST', body: credentials }); setSession(result.accessToken, result.user); return result.user } finally { isLoading.value = false } }
  async function loadCurrentUser() { const current = await api('/api/auth/me'); user.value = current; return current }
  async function restoreSession() { const token = localStorage.getItem(TOKEN_KEY); if (!token) return false; accessToken.value = token; isLoading.value = true; try { await loadCurrentUser(); return true } catch (error) { if (error.status === 401) clear(); else throw error; return false } finally { isLoading.value = false } }
  configureApi({ getToken: () => accessToken.value, onUnauthorized: clear })
  return { user, accessToken, isLoading, isAuthenticated, isAdmin, isCollaborator, login, logout, loadCurrentUser, restoreSession, clear }
})
