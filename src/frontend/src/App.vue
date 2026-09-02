<script setup>
import { onMounted } from 'vue'
import { useAuthStore } from './app/stores/auth.store'
const auth = useAuthStore()
onMounted(() => auth.restoreSession())
</script>
<template><RouterView /></template>
