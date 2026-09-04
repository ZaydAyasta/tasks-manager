<script setup>
import { computed, onMounted, ref } from 'vue'
import { apiMessage } from '../../app/services/api'
import AsyncState from '../../shared/components/AsyncState.vue'
import DashboardAttentionItem from './components/DashboardAttentionItem.vue'
import DashboardProjectCard from './components/DashboardProjectCard.vue'
import { dashboardService } from './dashboard.service'

const dashboard = ref(null)
const loading = ref(true)
const error = ref('')

const summaryCards = computed(() => dashboard.value ? [
  { label: 'Proyectos activos', value: dashboard.value.summary.activeProjects },
  { label: 'Tareas activas', value: dashboard.value.summary.activeTasks },
  { label: 'Bloqueadas', value: dashboard.value.summary.blockedTasks, tone: 'danger' },
  { label: 'En revisión', value: dashboard.value.summary.inReviewTasks, tone: 'warning' },
  { label: 'Vencidas', value: dashboard.value.summary.overdueTasks, tone: 'danger' },
  { label: 'Críticas', value: dashboard.value.summary.criticalTasks, tone: 'danger' }
] : [])

const empty = computed(() => !loading.value && !error.value && dashboard.value?.projects.length === 0)

async function load() {
  loading.value = true
  error.value = ''

  try {
    dashboard.value = await dashboardService.getDashboard()
  } catch (requestError) {
    error.value = apiMessage(requestError)
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="page dashboard-page">
    <header class="page-header dashboard-header">
      <div>
        <p class="eyebrow">VISTA OPERATIVA</p>
        <h1>Dashboard</h1>
        <p>Resumen del trabajo actual y de lo que requiere atención.</p>
      </div>
    </header>

    <AsyncState
      :loading="loading"
      :error="error"
      :empty="empty"
      empty-title="No hay proyectos todavía."
      empty-description="Crea el primer proyecto para empezar a organizar el trabajo."
    >
      <template #default>
        <section class="dashboard-summary" aria-label="Resumen del trabajo actual">
          <article v-for="card in summaryCards" :key="card.label" class="dashboard-summary-card" :class="card.tone">
            <span>{{ card.label }}</span>
            <strong>{{ card.value }}</strong>
          </article>
        </section>

        <section class="dashboard-section">
          <header class="dashboard-section-header">
            <div>
              <p class="eyebrow">PROYECTOS</p>
              <h2>Estado por proyecto</h2>
            </div>
            <RouterLink to="/projects" class="text-button">Ver proyectos</RouterLink>
          </header>
          <div class="dashboard-project-grid">
            <DashboardProjectCard v-for="project in dashboard.projects" :key="project.id" :project="project" />
          </div>
        </section>

        <section class="dashboard-section attention-section">
          <header class="dashboard-section-header">
            <div>
              <p class="eyebrow">REQUIEREN ATENCIÓN</p>
              <h2>Trabajo crítico y bloqueos</h2>
            </div>
            <span class="attention-count">{{ dashboard.attention.length }} elementos</span>
          </header>
          <p v-if="!dashboard.attention.length" class="inline-empty">No hay tareas que requieran atención ahora.</p>
          <div v-else class="dashboard-attention-list">
            <DashboardAttentionItem v-for="item in dashboard.attention" :key="item.taskId" :item="item" />
          </div>
        </section>
      </template>
    </AsyncState>

    <RouterLink v-if="empty" to="/projects" class="button primary empty-cta">Crear proyecto</RouterLink>
  </section>
</template>
