<script setup>
defineProps({ value: String, type: { type: String, default: 'status' } })
const label = value => ({ Pending: 'Pendiente', InProgress: 'En progreso', InReview: 'En revisión', Blocked: 'Bloqueada', Completed: 'Completada', Cancelled: 'Cancelada', High: 'Alta', Medium: 'Media', Low: 'Baja', Critical: 'Crítica', Active: 'Activo', Paused: 'Pausado' }[value] || value)
</script>
<template><span :class="['badge', type, value?.toLowerCase()]">{{ label(value) }}</span></template>
