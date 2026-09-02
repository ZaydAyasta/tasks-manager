<script setup>
import Badges from '../../shared/components/Badges.vue'
defineProps({ task: Object })
const dueLabel = task => { if (!task.dueDate) return null; const due = new Date(task.dueDate); const today = new Date(); today.setHours(0, 0, 0, 0); return due < today ? 'Vencida' : due.toLocaleDateString('es-PE') }
</script>
<template><RouterLink :to="`/tasks/${task.id}`" class="work-task-card"><header><div><Badges :value="task.status" /><Badges :value="task.priority" type="priority" /></div><span v-if="task.status === 'Blocked'" class="work-blocked">Bloqueada</span></header><strong>{{ task.title }}</strong><p class="work-context">{{ task.project.name }} <span>·</span> {{ task.stage.name }}</p><footer><span v-if="dueLabel(task)" :class="{ overdue: dueLabel(task) === 'Vencida' }">{{ dueLabel(task) }}</span><span>{{ task.subtaskProgress.completed }}/{{ task.subtaskProgress.total }} subtareas</span><span v-if="task.pendingDependencyCount">Esperando {{ task.pendingDependencyCount }} dependencias</span><span v-if="task.activeBlockerCount" class="blocker-count">{{ task.activeBlockerCount }} bloqueo{{ task.activeBlockerCount > 1 ? 's' : '' }}</span></footer></RouterLink></template>
