<script setup>
import { computed, onMounted, ref } from 'vue'
import AsyncState from '../../shared/components/AsyncState.vue'; import WorkTaskCard from './WorkTaskCard.vue'; import { workService } from './work.service'; import { apiMessage } from '../../app/services/api'
const items = ref([]); const loading = ref(true); const error = ref(''); const order = ['Blocked', 'InProgress', 'Pending', 'InReview']; const labels = { Blocked: 'Bloqueadas', InProgress: 'En progreso', Pending: 'Pendientes', InReview: 'En revisión' }
const groups = computed(() => Object.fromEntries(order.map(status => [status, items.value.filter(item => item.status === status)])))
const summary = computed(() => ({ pending: groups.value.Pending.length, progress: groups.value.InProgress.length, blocked: groups.value.Blocked.length }))
async function load() { loading.value = true; try { items.value = (await workService.getMyWork()).items || [] } catch (e) { error.value = apiMessage(e) } finally { loading.value = false } }
onMounted(load)
</script>
<template><section class="page"><header class="page-header"><div><p class="eyebrow">FOCO PERSONAL</p><h1>Mi trabajo</h1><p>Prioriza lo bloqueado y continúa con tu siguiente tarea.</p></div><div v-if="!loading && items.length" class="work-summary"><span><b>{{ summary.blocked }}</b> bloqueadas</span><span><b>{{ summary.progress }}</b> en progreso</span><span><b>{{ summary.pending }}</b> pendientes</span></div></header><AsyncState :loading="loading" :error="error" :empty="!items.length" empty-title="No tienes tareas asignadas" empty-description="Las tareas que se te asignen aparecerán aquí."><div class="work-sections"><section v-for="status in order" v-show="groups[status].length" :key="status" class="work-group"><header><span>{{ String(groups[status].length).padStart(2, '0') }}</span><h2>{{ labels[status] }}</h2></header><WorkTaskCard v-for="task in groups[status]" :key="task.id" :task="task" /></section></div></AsyncState></section></template>
