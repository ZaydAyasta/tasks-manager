<script setup>
import { computed } from 'vue'

const props = defineProps({
  project: { type: Object, required: true }
})

const progress = computed(() => {
  const { completed, total } = props.project.taskProgress
  return total === 0 ? 0 : Math.round((completed / total) * 100)
})
</script>

<template>
  <RouterLink :to="`/projects/${project.id}`" class="dashboard-project-card">
    <header>
      <div>
        <span class="badge" :class="project.status.toLowerCase()">{{ project.status }}</span>
        <h3>{{ project.name }}</h3>
      </div>
      <strong>{{ progress }}%</strong>
    </header>

    <div class="project-progress" role="progressbar" :aria-valuenow="progress" aria-valuemin="0" aria-valuemax="100">
      <span :style="{ width: `${progress}%` }"></span>
    </div>
    <p class="project-progress-copy">{{ project.taskProgress.completed }} de {{ project.taskProgress.total }} tareas completadas</p>

    <dl class="dashboard-project-signals">
      <div><dt>Bloqueadas</dt><dd>{{ project.blockedCount }}</dd></div>
      <div><dt>En revisión</dt><dd>{{ project.inReviewCount }}</dd></div>
      <div><dt>Vencidas</dt><dd>{{ project.overdueCount }}</dd></div>
    </dl>
  </RouterLink>
</template>
