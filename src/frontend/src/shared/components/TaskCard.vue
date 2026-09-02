<script setup>
import Badges from './Badges.vue'
defineProps({ task: Object })
</script>
<template><RouterLink :to="`/tasks/${task.id}`" class="task-card"><div class="task-card-top"><div class="card-badges"><Badges :value="task.status" /><Badges :value="task.priority" type="priority" /></div><span v-if="task.status === 'Blocked'" class="blocked-flag">Bloqueada</span></div><strong>{{ task.title }}</strong><p v-if="task.description">{{ task.description }}</p><div class="task-meta"><span v-if="task.dueDate">{{ new Date(task.dueDate).toLocaleDateString('es-PE') }}</span><span>{{ task.subtaskProgress?.completed || 0 }}/{{ task.subtaskProgress?.total || 0 }} subtareas</span><span v-if="task.pendingDependencyCount" class="dependency-count">{{ task.pendingDependencyCount }} dep.</span></div><div v-if="task.assignees?.length" class="avatars"><span v-for="person in task.assignees" :key="person.id" :title="person.fullName">{{ person.fullName.slice(0, 1) }}</span></div></RouterLink></template>
