<script setup>
import { computed } from 'vue'

const props = defineProps({
  item: { type: Object, required: true }
})

const dueDate = computed(() => props.item.dueDate ? new Date(props.item.dueDate) : null)
const isOverdue = computed(() => dueDate.value && dueDate.value < new Date())
const formattedDueDate = computed(() => dueDate.value
  ? new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium' }).format(dueDate.value)
  : 'Sin fecha límite')
</script>

<template>
  <RouterLink :to="`/tasks/${item.taskId}`" class="dashboard-attention-item">
    <div class="attention-main">
      <div class="attention-title-row">
        <strong>{{ item.title }}</strong>
        <span class="badge" :class="item.status.toLowerCase()">{{ item.status }}</span>
        <span class="badge" :class="item.priority.toLowerCase()">{{ item.priority }}</span>
      </div>
      <p>{{ item.projectName }}</p>
      <div class="attention-reasons">
        <span v-if="item.status === 'Blocked'" class="attention-reason blocked">Bloqueada</span>
        <span v-if="isOverdue" class="attention-reason overdue">Vencida</span>
        <span v-if="item.status === 'InReview'" class="attention-reason review">En revisión</span>
        <span v-if="item.priority === 'Critical'" class="attention-reason critical">Crítica</span>
      </div>
    </div>
    <div class="attention-meta">
      <span>{{ formattedDueDate }}</span>
      <span v-if="item.assignees.length">{{ item.assignees.map(assignee => assignee.fullName).join(', ') }}</span>
      <span v-else>Sin responsables</span>
    </div>
  </RouterLink>
</template>
