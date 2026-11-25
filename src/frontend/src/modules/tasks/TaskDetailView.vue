<script setup>
import { computed, ref, watch } from 'vue'
import { useAuthStore } from '../../app/stores/auth.store'
import { tasksService, workflowActions } from './tasks.service'
import { commentsService } from './comments.service'
import { attachmentsService } from './attachments.service'
import { projectsService } from '../projects/projects.service'
import { taskPayload, runAdminMutation } from '../../app/services/admin-ui'
import { activityLabels, appendUnique, blockerLabels } from '../../shared/presentation'
import AsyncState from '../../shared/components/AsyncState.vue'
import Badges from '../../shared/components/Badges.vue'
import BaseModal from '../../shared/components/BaseModal.vue'
import TaskForm from './TaskForm.vue'
import MemberSelector from '../projects/MemberSelector.vue'

const props = defineProps({ taskId: String })
const auth = useAuthStore()
const task = ref(null)
const subtasks = ref([])
const blockers = ref([])
const dependencies = ref([])
const dependents = ref([])
const activity = ref([])
const comments = ref([])
const attachments = ref([])
const members = ref([])
const stages = ref([])
const projectTasks = ref([])
const loading = ref(true)
const error = ref('')
const busy = ref(false)
const uploading = ref(false)
const paging = ref('')
const activityCursor = ref(null)
const commentCursor = ref(null)
const subtaskTitle = ref('')
const blockerType = ref('TechnicalIssue')
const blockerDescription = ref('')
const commentContent = ref('')
const editingComment = ref(null)
const selectedFile = ref(null)
const fileInput = ref(null)
const editModal = ref(false)
const moveStageId = ref('')
const dependencyTaskId = ref('')
const canManage = computed(() => auth.isAdmin)
const canWork = computed(() => canManage.value || task.value?.assignees?.some(person => person.id === auth.user?.id))
const editable = computed(() => ['Pending', 'InProgress', 'Blocked'].includes(task.value?.status))
const mutable = computed(() => canWork.value && editable.value)
const terminal = computed(() => ['Completed', 'Cancelled'].includes(task.value?.status))
const activeBlockers = computed(() => blockers.value.filter(item => !item.isResolved))
const availableAssigneeMembers = computed(() => members.value.map(member => ({ ...member, id: member.userId, isActive: true })))
const activeStages = computed(() => stages.value.filter(stage => stage.isActive && stage.id !== task.value?.stageId))
const actions = computed(() => canWork.value ? workflowActions(task.value?.status, canManage.value) : [])
const availableDependencies = computed(() => projectTasks.value.filter(item => item.id !== task.value?.id && !dependencies.value.some(dependency => dependency.dependsOnTask.id === item.id)))
const canDeleteComment = comment => comment.author.id === auth.user?.id || canManage.value
const canDeleteAttachment = attachment => attachment.uploadedBy.id === auth.user?.id || canManage.value
const formatFileSize = bytes => bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`

async function load() {
  const requestedTaskId = props.taskId
  loading.value = true
  try {
    const data = await Promise.all([
      tasksService.get(requestedTaskId), tasksService.subtasks(requestedTaskId), tasksService.blockers(requestedTaskId),
      tasksService.dependencies(requestedTaskId), tasksService.dependents(requestedTaskId), tasksService.activity(requestedTaskId),
      commentsService.list(requestedTaskId), attachmentsService.list(requestedTaskId)
    ])
    if (requestedTaskId !== props.taskId) return
    ;[task.value, subtasks.value, blockers.value, dependencies.value, dependents.value] = data
    activity.value = data[5].items || []; activityCursor.value = data[5].nextCursor
    comments.value = data[6].items || []; commentCursor.value = data[6].nextCursor
    attachments.value = data[7] || []
    if (canManage.value) [members.value, stages.value] = await Promise.all([
      projectsService.members(task.value.projectId), projectsService.stages(task.value.projectId)
    ])
    if (canWork.value) projectTasks.value = await projectsService.tasks(task.value.projectId)
  } catch (cause) { if (requestedTaskId === props.taskId) error.value = cause.message }
  finally { if (requestedTaskId === props.taskId) loading.value = false }
}
async function run(operation) {
  if (busy.value) return
  busy.value = true; error.value = ''
  try { await runAdminMutation(operation, load, value => { error.value = value }) }
  finally { busy.value = false }
}
async function loadMore(kind) {
  if (paging.value) return
  paging.value = kind; error.value = ''
  try {
    if (kind === 'activity') {
      const feed = await tasksService.activity(task.value.id, activityCursor.value)
      activity.value = appendUnique(activity.value, feed.items); activityCursor.value = feed.nextCursor
    } else {
      const feed = await commentsService.list(task.value.id, commentCursor.value)
      comments.value = appendUnique(comments.value, feed.items); commentCursor.value = feed.nextCursor
    }
  } catch (cause) { error.value = cause.message }
  finally { paging.value = '' }
}
async function comment() {
  if (commentContent.value.trim()) await run(async () => { await commentsService.create(task.value.id, commentContent.value); commentContent.value = '' })
}
async function updateComment(comment) {
  await run(async () => { await commentsService.update(task.value.id, comment.id, editingComment.value.content); editingComment.value = null })
}
async function upload() {
  if (!selectedFile.value || busy.value) return
  uploading.value = true
  try { await run(async () => { await attachmentsService.upload(task.value.id, selectedFile.value); selectedFile.value = null; if (fileInput.value) fileInput.value.value = '' }) }
  finally { uploading.value = false }
}
async function download(attachment) {
  try {
    const blob = await attachmentsService.download(`/api/tasks/${task.value.id}/attachments/${attachment.id}/download`)
    const url = URL.createObjectURL(blob); const link = document.createElement('a')
    link.href = url; link.download = attachment.fileName; link.click(); URL.revokeObjectURL(url)
  } catch (cause) { error.value = cause.message }
}
async function cancel() {
  if (window.confirm('¿Cancelar esta tarea? No podrá continuar su flujo de trabajo.'))
    await run(() => tasksService.workflow(task.value.id, 'cancel', task.value.version))
}
async function addDependency() {
  await run(async () => { await tasksService.addDependency(task.value.id, dependencyTaskId.value, task.value.version); dependencyTaskId.value = '' })
}
function removeDependency(item) {
  if (window.confirm('¿Quitar esta dependencia?'))
    run(() => tasksService.removeDependency(task.value.id, item.id, task.value.version))
}
watch(() => props.taskId, () => {
  task.value = null; error.value = ''; editModal.value = false; editingComment.value = null
  selectedFile.value = null; moveStageId.value = ''; dependencyTaskId.value = ''; commentContent.value = ''; subtaskTitle.value = ''; blockerDescription.value = ''
  load()
}, { immediate: true })
</script>

<template>
  <section class="page task-page">
    <AsyncState :loading="loading && !task" :error="!task ? error : ''" :empty="false">
      <header class="page-header task-header">
        <div><RouterLink :to="`/projects/${task.projectId}`" class="back-link">Volver al tablero</RouterLink><h1>{{ task.title }}</h1><div class="inline-badges"><Badges :value="task.status" /><Badges :value="task.priority" type="priority" /></div></div>
        <div class="action-row">
          <button v-if="canManage" class="button secondary" :disabled="busy" @click="editModal = true">Editar</button>
          <button v-for="action in actions" :key="action.id" :class="['button', action.secondary ? 'secondary' : 'primary']" :disabled="busy" @click="run(() => tasksService.workflow(task.id, action.id, task.version))">{{ action.label }}</button>
          <button v-if="canManage && !terminal" class="danger-button" :disabled="busy" @click="cancel">Cancelar tarea</button>
        </div>
      </header>
      <p class="description">{{ task.description || 'Esta tarea no tiene descripción.' }}</p>
      <p v-if="!canWork" class="muted">Puedes consultar y colaborar. Para gestionar el trabajo debes estar asignado a esta tarea.</p>
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <p v-if="busy && !uploading" class="muted" role="status">Guardando cambios...</p>
      <div class="detail-grid">
        <section v-if="canManage" class="detail-section">
          <header><h2>Administración</h2></header>
          <label class="move-select">Mover a etapa<select v-model="moveStageId"><option value="">Selecciona una etapa</option><option v-for="stage in activeStages" :key="stage.id" :value="stage.id">{{ stage.name }}</option></select></label>
          <button class="button secondary" :disabled="busy || !moveStageId" @click="run(async () => { await tasksService.move(task.id, moveStageId, task.version); moveStageId = '' })">Mover tarea</button>
          <h3>Responsables</h3>
          <div class="assignee-chips"><span v-for="person in task.assignees" :key="person.id" class="assignee-chip">{{ person.fullName }}<button :aria-label="`Quitar asignación de ${person.fullName}`" :disabled="busy" @click="run(() => tasksService.removeAssignee(task.id, person.id, task.version))">Quitar</button></span></div>
          <MemberSelector :users="availableAssigneeMembers" :members="task.assignees.map(item => ({ ...item, userId: item.id }))" :submitting="busy" @add="userId => run(() => tasksService.addAssignee(task.id, userId, task.version))" />
        </section>
        <section class="detail-section">
          <header><h2>Subtareas <small>{{ task.subtaskProgress.completed }}/{{ task.subtaskProgress.total }}</small></h2></header>
          <form v-if="mutable" class="compact-form" @submit.prevent="run(async () => { await tasksService.createSubtask(task.id, subtaskTitle, task.version); subtaskTitle = '' })">
            <input v-model="subtaskTitle" aria-label="Nueva subtarea" placeholder="Añadir subtarea" required /><button class="button secondary" :disabled="busy">Añadir</button>
          </form>
          <ul class="check-list">
            <li v-for="item in subtasks" :key="item.id">
              <input v-if="mutable" type="checkbox" :aria-label="`Completar subtarea ${item.title}`" :checked="item.isCompleted" :disabled="busy" @change="run(() => tasksService.subtaskAction(task.id, item.id, item.isCompleted ? 'reopen' : 'complete', task.version))" />
              <span :class="{ done: item.isCompleted }">{{ item.title }}</span>
              <button v-if="mutable" class="text-button danger" :disabled="busy" @click="run(() => tasksService.deleteSubtask(task.id, item.id, task.version))">Eliminar</button>
            </li><li v-if="!subtasks.length" class="muted">No hay subtareas.</li>
          </ul>
        </section>
        <section class="detail-section">
          <header><h2>Bloqueos</h2></header>
          <div v-if="activeBlockers.length" class="blocker-list"><article v-for="blocker in activeBlockers" :key="blocker.id"><b>{{ blockerLabels[blocker.type] || 'Otro' }}</b><p>{{ blocker.description }}</p><button v-if="mutable" class="text-button" :disabled="busy" @click="run(() => tasksService.resolveBlocker(task.id, blocker.id, task.version))">Resolver</button></article></div>
          <p v-else class="muted">No hay bloqueos activos.</p>
          <form v-if="mutable" class="blocker-form" @submit.prevent="run(async () => { await tasksService.reportBlocker(task.id, blockerType, blockerDescription, task.version); blockerDescription = '' })">
            <label class="move-select">Tipo de bloqueo<select v-model="blockerType"><option v-for="(label, value) in blockerLabels" :key="value" :value="value">{{ label }}</option></select></label>
            <textarea v-model="blockerDescription" aria-label="Descripción del bloqueo" placeholder="Describe el bloqueo" required /><button class="button secondary" :disabled="busy">Reportar bloqueo</button>
          </form>
        </section>
        <section class="detail-section">
          <header><h2>Dependencias</h2></header>
          <p class="muted">Los prerrequisitos deben estar completados para iniciar, revisar o completar esta tarea.</p>
          <ul class="link-list"><li v-for="item in dependencies" :key="item.id"><RouterLink :to="`/tasks/${item.dependsOnTask.id}`">{{ item.dependsOnTask.title }}</RouterLink><Badges :value="item.dependsOnTask.status" /><button v-if="mutable" class="text-button danger" :disabled="busy" @click="removeDependency(item)">Quitar</button></li><li v-if="!dependencies.length" class="muted">No depende de otras tareas.</li></ul>
          <form v-if="mutable" class="admin-form" @submit.prevent="addDependency">
            <label>Prerrequisito<select v-model="dependencyTaskId" required><option value="">Selecciona una tarea</option><option v-for="item in availableDependencies" :key="item.id" :value="item.id">{{ item.title }}</option></select></label>
            <button class="button secondary" :disabled="busy || !dependencyTaskId">Agregar dependencia</button>
          </form>
          <h3 v-if="dependents.length">Tareas que dependen de esta</h3>
          <ul v-if="dependents.length" class="link-list"><li v-for="item in dependents" :key="item.dependencyId"><RouterLink :to="`/tasks/${item.task.id}`">{{ item.task.title }}</RouterLink></li></ul>
        </section>
        <section class="detail-section attachments">
          <header><h2>Archivos</h2></header>
          <label class="file-picker">Adjuntar archivo<input ref="fileInput" type="file" @change="selectedFile = $event.target.files?.[0] || null" /></label>
          <button class="button secondary" :disabled="busy || !selectedFile" @click="upload">{{ uploading ? 'Subiendo archivo...' : 'Subir archivo' }}</button>
          <p v-if="selectedFile" class="muted">{{ selectedFile.name }}</p>
          <ul class="attachment-list"><li v-for="attachment in attachments" :key="attachment.id"><div><b>{{ attachment.fileName }}</b><small>{{ attachment.contentType }} · {{ formatFileSize(attachment.sizeBytes) }} · {{ attachment.uploadedBy.fullName }}</small></div><button class="text-button" @click="download(attachment)">Descargar</button><button v-if="canDeleteAttachment(attachment)" class="text-button danger" :disabled="busy" @click="run(() => attachmentsService.remove(task.id, attachment.id))">Eliminar</button></li><li v-if="!attachments.length" class="muted">No hay archivos adjuntos.</li></ul>
        </section>
        <section class="detail-section comments">
          <header><h2>Comentarios</h2></header>
          <form class="comment-form" @submit.prevent="comment"><textarea v-model="commentContent" maxlength="4000" aria-label="Nuevo comentario" placeholder="Escribe una actualización para el equipo" required /><button class="button primary" :disabled="busy">Comentar</button></form>
          <ol class="comment-list"><li v-for="item in comments" :key="item.id"><header><b>{{ item.author.fullName }}</b><small>{{ new Date(item.createdAt).toLocaleString('es-PE') }} <em v-if="item.isEdited">Editado</em></small></header><textarea v-if="editingComment?.id === item.id" v-model="editingComment.content" aria-label="Editar comentario" maxlength="4000" /><p v-else>{{ item.content }}</p><div v-if="canDeleteComment(item)"><button v-if="item.author.id === auth.user?.id" class="text-button" @click="editingComment = { ...item }">Editar</button><button v-if="editingComment?.id === item.id" class="text-button" :disabled="busy" @click="updateComment(item)">Guardar</button><button class="text-button danger" :disabled="busy" @click="run(() => commentsService.remove(task.id, item.id))">Eliminar</button></div></li><li v-if="!comments.length" class="muted">Aún no hay comentarios.</li></ol>
          <button v-if="commentCursor" class="button secondary" :disabled="Boolean(paging)" @click="loadMore('comments')">{{ paging === 'comments' ? 'Cargando...' : 'Cargar más comentarios' }}</button>
        </section>
        <section class="detail-section activity">
          <header><h2>Actividad</h2></header>
          <ol><li v-for="item in activity" :key="item.id"><span class="activity-dot"></span><div><b>{{ item.actor.fullName }}</b> {{ activityLabels[item.activityType] || 'actualizó el trabajo' }}<small>{{ new Date(item.occurredAt).toLocaleString('es-PE') }}</small></div></li><li v-if="!activity.length" class="muted">Aún no hay actividad registrada.</li></ol>
          <button v-if="activityCursor" class="button secondary" :disabled="Boolean(paging)" @click="loadMore('activity')">{{ paging === 'activity' ? 'Cargando...' : 'Cargar más actividad' }}</button>
        </section>
      </div>
      <BaseModal v-model="editModal" title="Editar tarea"><TaskForm :task="task" :submitting="busy" @submit="form => run(async () => { await tasksService.update(task.id, taskPayload(form, task.version)); editModal = false })" /></BaseModal>
    </AsyncState>
  </section>
</template>
