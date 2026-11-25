<script setup>
import { computed, onMounted, ref } from 'vue'
import { useAuthStore } from '../../app/stores/auth.store'; import { projectsService } from './projects.service'; import { stagesService } from './stages.service'; import { usersService } from './users.service'; import { tasksService } from '../tasks/tasks.service'; import { adminErrorMessage, projectPayload, runAdminMutation, taskPayload } from '../../app/services/admin-ui'; import AsyncState from '../../shared/components/AsyncState.vue'; import Badges from '../../shared/components/Badges.vue'; import BaseModal from '../../shared/components/BaseModal.vue'; import ProjectForm from './ProjectForm.vue'; import StageForm from './StageForm.vue'; import TaskForm from '../tasks/TaskForm.vue'; import ProjectMembersPanel from './ProjectMembersPanel.vue'; import StageMembersPanel from './StageMembersPanel.vue'
import ProjectActivity from './ProjectActivity.vue'
import StageBoard from './StageBoard.vue'
import { calendarDate } from '../../shared/presentation'
const props = defineProps({ projectId: String }); const auth = useAuthStore(); const project = ref(null); const stages = ref([]); const tasks = ref([]); const members = ref([]); const users = ref([]); const stageMembers = ref({}); const loading = ref(true); const error = ref(''); const activeTab = ref('board'); const busy = ref(false); const editProject = ref(false); const stageModal = ref(null); const taskModal = ref(null)
const canManage = computed(() => auth.isAdmin); const editable = computed(() => ['Active', 'Paused'].includes(project.value?.status)); const tabs = computed(() => [
  { id: 'board', label: 'Tablero' },
  { id: 'members', label: 'Equipo' },
  ...(canManage.value ? [{ id: 'settings', label: 'Configurar etapas' }] : []),
  { id: 'activity', label: 'Historial' }
])
const stageMemberList = stage => stageMembers.value[stage.id] || []
const orderFeedback = ref('')
const orderError = ref('')
async function load() { loading.value = true; try { const values = await Promise.all([projectsService.get(props.projectId), projectsService.stages(props.projectId), projectsService.tasks(props.projectId), projectsService.members(props.projectId), canManage.value ? usersService.list() : Promise.resolve([])]); [project.value, stages.value, tasks.value, members.value, users.value] = values; if (canManage.value) { const pairs = await Promise.all(stages.value.map(async stage => [stage.id, await stagesService.members(props.projectId, stage.id)])); stageMembers.value = Object.fromEntries(pairs) } } catch (e) { error.value = e.message } finally { loading.value = false } }
async function mutate(operation) { busy.value = true; const ok = await runAdminMutation(operation, load, message => error.value = message); busy.value = false; return ok }
async function saveProject(form) { if (await mutate(() => projectsService.update(project.value.id, { ...projectPayload(form), version: project.value.version }))) editProject.value = false }
async function lifecycle(action, terminal = false) { if (terminal && !window.confirm(`¿${action === 'complete' ? 'Completar' : 'Cancelar'} este proyecto?`)) return; await mutate(() => projectsService.lifecycle(project.value.id, action, project.value.version)) }
async function saveStage(form) { const current = stageModal.value; const operation = current?.stage ? () => stagesService.update(project.value.id, current.stage.id, { ...form, version: current.stage.version }) : () => stagesService.create(project.value.id, form); if (await mutate(operation)) stageModal.value = null }
async function reorder(stageId, target) {
  if (!canManage.value || !editable.value || busy.value || target < 0 || target >= stages.value.length) return

  const index = stages.value.findIndex(stage => stage.id === stageId)
  if (index < 0 || index === target) return

  const ordered = [...stages.value]
  const [moved] = ordered.splice(index, 1)
  ordered.splice(target, 0, moved)
  const payload = ordered.map((stage, position) => ({ stageId: stage.id, position: position + 1, version: stage.version }))

  busy.value = true
  orderError.value = ''
  orderFeedback.value = 'Guardando el orden...'
  try {
    await stagesService.reorder(props.projectId, payload)
    try {
      stages.value = await projectsService.stages(props.projectId)
      orderFeedback.value = `${moved.name} quedó en la posición ${target + 1}.`
    } catch {
      orderFeedback.value = ''
      orderError.value = 'El orden se guardó. Recarga la página para ver la posición actual.'
    }
  } catch (cause) {
    orderFeedback.value = ''
    orderError.value = adminErrorMessage(cause)
    if (cause?.status === 409) {
      try { stages.value = await projectsService.stages(props.projectId) }
      catch { orderError.value = 'El orden cambió. Recarga la página antes de mover una etapa.' }
    }
  } finally {
    busy.value = false
  }
}
async function saveTask(form) { const stage = taskModal.value; if (await mutate(() => tasksService.create(project.value.id, { ...taskPayload(form), stageId: stage.id, assigneeIds: [] }))) taskModal.value = null }
async function removeMember(member) { if (!window.confirm(`¿Quitar a ${member.fullName} del proyecto?`)) return; await mutate(() => projectsService.removeMember(project.value.id, member.userId)) }
async function addStageMember(stage, userId, role) { await mutate(() => stagesService.addMember(project.value.id, stage.id, userId, role)) }
async function removeStageMember(stage, member) { await mutate(() => stagesService.removeMember(project.value.id, stage.id, member.userId)) }
onMounted(load)
</script>
<template><section class="page"><AsyncState :loading="loading" :error="error" :empty="false"><header class="page-header board-header"><div><RouterLink to="/projects" class="back-link">← Proyectos</RouterLink><div class="title-line"><h1>{{ project.name }}</h1><Badges :value="project.status" type="project" /></div><p>{{ project.description || 'Sin descripción del proyecto.' }}</p><p class="project-dates">{{ calendarDate(project.startDate) || 'Sin inicio' }} — {{ calendarDate(project.endDate) || 'Sin fin' }}</p></div><div v-if="canManage" class="action-row"><button class="button secondary" :disabled="busy || !editable" @click="editProject = true">Editar</button><button v-if="project.status === 'Active'" class="button secondary" :disabled="busy" @click="lifecycle('pause')">Pausar</button><button v-if="project.status === 'Paused'" class="button secondary" :disabled="busy" @click="lifecycle('resume')">Reanudar</button><button v-if="editable" class="button secondary" :disabled="busy" @click="lifecycle('complete', true)">Completar</button><button v-if="editable" class="button danger-button" :disabled="busy" @click="lifecycle('cancel', true)">Cancelar</button></div></header><p v-if="error" class="form-error">{{ error }}</p><nav class="local-tabs" aria-label="Vistas del proyecto"><button v-for="tab in tabs" :key="tab.id" type="button" :aria-pressed="activeTab === tab.id" :class="{ active: activeTab === tab.id }" @click="activeTab = tab.id">{{ tab.label }}</button></nav><StageBoard
    v-if="activeTab === 'board'"
    :stages="stages"
    :tasks="tasks"
    :can-manage="canManage"
    :editable="editable"
    :busy="busy"
    :order-feedback="orderFeedback"
    :order-error="orderError"
    @add-stage="stageModal = {}"
    @edit-stage="stage => stageModal = { stage }"
    @toggle-stage="stage => mutate(() => stagesService.toggle(project.id, stage.id, stage.isActive ? 'deactivate' : 'activate', stage.version))"
    @add-task="stage => taskModal = stage"
    @reorder="reorder"
  />
  <ProjectMembersPanel v-else-if="activeTab === 'members'" :members="members" :users="users" :can-manage="canManage" :submitting="busy" @add="userId => mutate(() => projectsService.addMember(project.id, userId))" @remove="removeMember" /><ProjectActivity v-else-if="activeTab === 'activity'" :key="projectId" :project-id="projectId" /><section v-else class="config-panel"><header><div><p class="eyebrow">FLUJO</p><h2>Configuración de etapas</h2><p>Define quién participa en cada parte del proceso.</p></div></header><StageMembersPanel v-for="stage in stages" :key="stage.id" :stage="stage" :members="stageMemberList(stage)" :project-members="members" :can-manage="canManage" :submitting="busy" @add="addStageMember" @remove="removeStageMember" /></section><BaseModal v-model="editProject" title="Editar proyecto"><ProjectForm :project="project" :submitting="busy" @submit="saveProject" /></BaseModal><BaseModal :model-value="Boolean(stageModal)" :title="stageModal?.stage ? 'Editar etapa' : 'Nueva etapa'" @update:model-value="value => { if (!value) stageModal = null }"><StageForm :stage="stageModal?.stage" :submitting="busy" @submit="saveStage" /></BaseModal><BaseModal :model-value="Boolean(taskModal)" title="Nueva tarea" @update:model-value="value => { if (!value) taskModal = null }"><TaskForm :submitting="busy" @submit="saveTask" /></BaseModal></AsyncState></section></template>
