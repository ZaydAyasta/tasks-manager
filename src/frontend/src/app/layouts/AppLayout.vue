<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth.store'
const auth = useAuthStore(); const router = useRouter()
const navigation = computed(() => auth.isAdmin ? [{ to: '/projects', label: 'Proyectos' }, { to: '/my-work', label: 'Mi trabajo' }] : [{ to: '/my-work', label: 'Mi trabajo' }, { to: '/projects', label: 'Mis proyectos' }])
function logout() { auth.logout(); router.push('/login') }
</script>
<template>
  <div class="app-shell"><aside class="sidebar"><RouterLink to="/projects" class="brand"><span>N</span><b>Nakama</b></RouterLink><nav><RouterLink v-for="item in navigation" :key="item.to" :to="item.to">{{ item.label }}</RouterLink></nav><div class="sidebar-footer"><div class="user-label"><b>{{ auth.user?.fullName }}</b><small>{{ auth.user?.role }}</small></div><button class="text-button" @click="logout">Cerrar sesión</button></div></aside><main class="main-content"><RouterView /></main></div>
</template>
