<script setup>
import { onMounted, ref } from 'vue'
import { apiMessage } from '../../app/services/api'
import { activityLabels, appendUnique } from '../../shared/presentation'
import AsyncState from '../../shared/components/AsyncState.vue'
import { projectsService } from './projects.service'

const props = defineProps({ projectId: { type: String, required: true } })
const items = ref([])
const nextCursor = ref(null)
const loading = ref(false)
const error = ref('')
const dateFormatter = new Intl.DateTimeFormat('es-PE', { dateStyle: 'medium', timeStyle: 'short' })

async function load() {
  if (loading.value) return
  loading.value = true
  error.value = ''
  try {
    const page = await projectsService.activity(props.projectId, nextCursor.value)
    items.value = appendUnique(items.value, page.items)
    nextCursor.value = page.nextCursor
  } catch (cause) {
    error.value = apiMessage(cause)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="detail-section activity project-activity" aria-labelledby="project-history-title">
    <header><div><h2 id="project-history-title">Historial del proyecto</h2><p class="muted">Consulta qué cambió, quién lo hizo y cuándo.</p></div></header>
    <AsyncState :loading="loading && !items.length" :error="!items.length ? error : ''" :empty="!items.length" empty-title="Aún no hay cambios registrados" empty-description="Aquí aparecerá la actividad del proyecto y sus tareas.">
      <ol>
        <li v-for="event in items" :key="event.id">
          <span class="activity-dot" aria-hidden="true"></span>
          <div>
            <strong>{{ event.actor.fullName }}</strong> {{ activityLabels[event.activityType] || 'actualizó el proyecto' }}
            <span v-if="event.taskId"> &middot; <RouterLink :to="`/tasks/${event.taskId}`">{{ event.metadata?.taskTitle || event.metadata?.Title || 'Ver tarea' }}</RouterLink></span>
            <span v-else-if="event.metadata?.stageName"> &middot; {{ event.metadata.stageName }}</span>
            <time :datetime="event.occurredAt">{{ dateFormatter.format(new Date(event.occurredAt)) }}</time>
          </div>
        </li>
      </ol>
    </AsyncState>
    <p v-if="items.length && error" class="form-error" role="alert">{{ error }}</p>
    <button v-if="nextCursor || error" class="button secondary" type="button" :disabled="loading" @click="load">
      {{ loading ? 'Cargando historial...' : error ? 'Reintentar' : 'Cargar más cambios' }}
    </button>
  </section>
</template>
