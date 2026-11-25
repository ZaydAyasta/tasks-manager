<script setup>
defineProps({ value: String, type: { type: String, default: 'status' } })
const labels = { Pending: 'Pendiente', InProgress: 'En progreso', InReview: 'En revisión', Blocked: 'Bloqueada', Completed: 'Completada', Cancelled: 'Cancelada', High: 'Alta', Medium: 'Media', Low: 'Baja', Critical: 'Crítica', Active: 'Activo', Paused: 'Pausado' }
const projectLabels = { Completed: 'Completado', Cancelled: 'Cancelado' }
</script>
<template><span :class="['badge', type, value?.toLowerCase()]">{{ (type === 'project' && projectLabels[value]) || labels[value] || value }}</span></template>
