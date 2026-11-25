<script setup>
import { computed, onMounted, ref } from 'vue'
import AsyncState from '../../shared/components/AsyncState.vue'
import WorkTaskCard from './WorkTaskCard.vue'
import { workService } from './work.service'
import { projectsService } from '../projects/projects.service'
import { apiMessage } from '../../app/services/api'
import { appendUnique } from '../../shared/presentation'

const items = ref([])
const projects = ref([])
const loading = ref(true)
const loadingMore = ref(false)
const error = ref('')
const nextCursor = ref(null)
const filters = ref({ status: '', priority: '', projectId: '' })
const appliedFilters = ref({ ...filters.value })
const order = ['Blocked', 'InProgress', 'Pending', 'InReview', 'Completed', 'Cancelled']
const labels = { Blocked: 'Bloqueadas', InProgress: 'En progreso', Pending: 'Pendientes', InReview: 'En revisión', Completed: 'Completadas', Cancelled: 'Canceladas' }
const groups = computed(() => Object.fromEntries(order.map(status => [status, items.value.filter(item => item.status === status)])))
const summary = computed(() => ({ pending: groups.value.Pending.length, progress: groups.value.InProgress.length, blocked: groups.value.Blocked.length }))
const hasFilters = computed(() => Object.values(appliedFilters.value).some(Boolean))
async function load(append = false) {
  if (append && (!nextCursor.value || loading.value || loadingMore.value)) return
  if (append) loadingMore.value = true
  else { loading.value = true; items.value = []; nextCursor.value = null }
  error.value = ''
  try {
    const feed = await workService.getMyWork({ ...appliedFilters.value, ...(append ? { cursor: nextCursor.value } : {}) })
    items.value = append ? appendUnique(items.value, feed.items || []) : feed.items || []
    nextCursor.value = feed.nextCursor
  } catch (cause) { error.value = apiMessage(cause) }
  finally { loading.value = false; loadingMore.value = false }
}
async function applyFilters() {
  if (loading.value || loadingMore.value) return
  appliedFilters.value = { ...filters.value }
  await load()
}
async function reset() {
  filters.value = { status: '', priority: '', projectId: '' }
  await applyFilters()
}
onMounted(async () => {
  await load()
  try { projects.value = await projectsService.list() || [] } catch { /* The work feed remains usable when project options cannot be loaded. */ }
})
</script>
<template>
  <section class="page">
    <header class="page-header">
      <div><p class="eyebrow">FOCO PERSONAL</p><h1>Mi trabajo</h1><p>Prioriza lo bloqueado y continúa con tu siguiente tarea.</p></div>
      <div v-if="!loading && items.length" class="work-summary"><span><b>{{ summary.blocked }}</b> bloqueadas</span><span><b>{{ summary.progress }}</b> en progreso</span><span><b>{{ summary.pending }}</b> pendientes</span></div>
    </header>
    <search aria-label="Filtrar tareas">
      <form class="filter-bar" @submit.prevent="applyFilters">
        <label>Estado<select v-model="filters.status" name="status"><option value="">Trabajo activo</option><option v-for="status in order" :key="status" :value="status">{{ labels[status] }}</option></select></label>
        <label>Prioridad<select v-model="filters.priority" name="priority"><option value="">Todas</option><option value="Low">Baja</option><option value="Medium">Media</option><option value="High">Alta</option><option value="Critical">Crítica</option></select></label>
        <label>Proyecto<select v-model="filters.projectId" name="projectId"><option value="">Todos mis proyectos</option><option v-for="project in projects" :key="project.id" :value="project.id">{{ project.name }}</option></select></label>
        <button type="submit" class="button primary" :disabled="loading || loadingMore">Aplicar filtros</button>
        <button type="button" class="button secondary" :disabled="loading || loadingMore" @click="reset">Limpiar</button>
      </form>
    </search>
    <p v-if="items.length && error" class="form-error" role="alert">{{ error }}</p>
    <AsyncState :loading="loading" :error="!items.length ? error : ''" :empty="!items.length" :empty-title="hasFilters ? 'No hay tareas con estos filtros' : 'No tienes tareas asignadas'" empty-description="Las tareas que se te asignen aparecerán aquí.">
      <p class="muted">{{ items.length }} tareas cargadas. Los contadores corresponden a lo cargado.</p>
      <div class="work-sections"><section v-for="status in order" v-show="groups[status].length" :key="status" class="work-group"><header><span>{{ String(groups[status].length).padStart(2, '0') }}</span><h2>{{ labels[status] }}</h2></header><WorkTaskCard v-for="task in groups[status]" :key="task.id" :task="task" /></section></div>
    </AsyncState>
    <div v-if="nextCursor && !loading" class="pagination"><button class="button secondary" :disabled="loadingMore" @click="load(true)">{{ loadingMore ? 'Cargando...' : 'Cargar más tareas' }}</button></div>
  </section>
</template>
