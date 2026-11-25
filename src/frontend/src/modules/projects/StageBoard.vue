<script setup>
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import TaskCard from '../../shared/components/TaskCard.vue'

const props = defineProps({
  stages: { type: Array, required: true },
  tasks: { type: Array, required: true },
  canManage: Boolean,
  editable: Boolean,
  busy: Boolean,
  orderFeedback: String,
  orderError: String
})
const emit = defineEmits(['add-stage', 'edit-stage', 'toggle-stage', 'add-task', 'reorder'])
const board = ref(null)
const grabbedStage = ref(null)
const cancellation = ref('')
const canReorder = computed(() => props.canManage && props.editable && props.stages.length > 1)
const tasksByStage = computed(() => {
  const result = new Map()
  for (const task of props.tasks) {
    if (!result.has(task.stageId)) result.set(task.stageId, [])
    result.get(task.stageId).push(task)
  }
  return result
})

let drag = null
let frame = 0
let pendingMove = null

function columns() {
  return Array.from(board.value.children).filter(column => column.dataset.stageId)
}

function startDrag(event, index) {
  if (!canReorder.value || props.busy || drag || !event.isPrimary || event.button !== 0) return

  const handle = event.currentTarget
  handle.setPointerCapture(event.pointerId)
  drag = {
    pointerId: event.pointerId, handle, column: handle.closest('[data-stage-id]'), index,
    startX: event.clientX, startScroll: board.value.scrollLeft, x: event.clientX, time: null, target: index
  }
  cancellation.value = ''
  window.addEventListener('keydown', cancelWithEscape)
}

function moveDrag(event) {
  if (drag?.pointerId !== event.pointerId) return

  drag.x = event.clientX
  if (!grabbedStage.value && Math.abs(drag.x - drag.startX) < 6) return
  grabbedStage.value = drag.column.dataset.stageId
  if (!frame) frame = requestAnimationFrame(animateDrag)
}

function updateTarget() {
  const list = board.value
  const left = list.getBoundingClientRect().left + list.clientLeft
  const x = list.scrollLeft + Math.max(0, Math.min(list.clientWidth, drag.x - left))
  const siblings = columns().filter(column => column !== drag.column)
  const next = siblings.findIndex(column => x < column.offsetLeft + column.offsetWidth / 2)
  drag.target = next === -1 ? siblings.length : next

  list.toggleAttribute('data-stage-drop', drag.target !== drag.index)
  if (drag.target === drag.index) return

  const before = siblings[drag.target - 1]
  const after = siblings[drag.target]
  const previousEdge = before ? before.offsetLeft + before.offsetWidth : after.offsetLeft - 12
  const nextEdge = after ? after.offsetLeft : previousEdge + 12
  list.style.setProperty('--stage-drop-x', `${(previousEdge + nextEdge) / 2}px`)
}

function animateDrag(time) {
  const list = board.value
  const bounds = list.getBoundingClientRect()
  const edge = Math.min(64, list.clientWidth / 3)
  const intoLeft = Math.max(0, bounds.left + edge - drag.x)
  const intoRight = Math.max(0, drag.x - bounds.right + edge)
  const velocity = Math.max(-1, Math.min(1, (intoRight - intoLeft) / edge))
  const elapsed = drag.time === null ? 16 : Math.min(32, time - drag.time)
  drag.time = time
  list.scrollLeft += velocity * elapsed * 0.75
  drag.column.style.setProperty('--stage-drag-x', `${drag.x - drag.startX + list.scrollLeft - drag.startScroll}px`)
  updateTarget()
  frame = requestAnimationFrame(animateDrag)
}

function resetDrag() {
  if (!drag) return

  const { handle, pointerId, column } = drag
  drag = null
  cancelAnimationFrame(frame)
  frame = 0
  grabbedStage.value = null
  column.style.removeProperty('--stage-drag-x')
  board.value?.removeAttribute('data-stage-drop')
  board.value?.style.removeProperty('--stage-drop-x')
  window.removeEventListener('keydown', cancelWithEscape)
  if (handle.hasPointerCapture(pointerId)) handle.releasePointerCapture(pointerId)
}

function endDrag(event) {
  if (drag?.pointerId !== event.pointerId) return

  const { top, bottom } = board.value.getBoundingClientRect()
  if (!grabbedStage.value || event.clientY < top || event.clientY > bottom || props.busy) {
    resetDrag()
    return
  }

  drag.x = event.clientX
  updateTarget()
  const { index, target, column } = drag
  resetDrag()
  if (target !== index) requestMove(column.dataset.stageId, target)
}

function cancelDrag(event) {
  if (drag?.pointerId === event.pointerId) resetDrag()
}

function cancelWithEscape(event) {
  if (event.key !== 'Escape') return
  const column = drag.column
  if (grabbedStage.value) cancellation.value = 'Se canceló el cambio de orden.'
  resetDrag()
  column.scrollIntoView({ block: 'nearest', inline: 'nearest' })
}

function requestMove(stageId, target, button = null) {
  if (!canReorder.value || props.busy || drag || target < 0 || target >= props.stages.length) return
  pendingMove = { stageId, button }
  cancellation.value = ''
  emit('reorder', stageId, target)
}

watch(() => props.stages.map(stage => stage.id).join(','), () => {
  if (!pendingMove || !board.value) return
  const { stageId, button } = pendingMove
  const column = columns().find(item => item.dataset.stageId === stageId)
  column?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
  if (button?.isConnected && document.activeElement === document.body) button.focus({ preventScroll: true })
}, { flush: 'post' })

watch(() => props.busy, value => {
  if (!value) pendingMove = null
}, { flush: 'post' })

onBeforeUnmount(resetDrag)
</script>

<template>
  <div>
    <div v-if="canManage && editable" class="board-actions stage-board-actions">
      <p v-if="canReorder" class="muted">Arrastra una etapa desde los seis puntos o usa las flechas para cambiar su orden.</p>
      <button type="button" class="button secondary" :disabled="busy" @click="emit('add-stage')">+ Agregar etapa</button>
    </div>
    <p class="stage-order-feedback muted" role="status">{{ cancellation || orderFeedback }}</p>
    <p v-if="orderError" class="stage-order-feedback form-error" role="alert">{{ orderError }}</p>

    <div v-if="!stages.length" class="state-card">
      <b>Este proyecto todavía no tiene etapas.</b>
      <span>Organiza el flujo de trabajo antes de añadir tareas.</span>
      <button v-if="canManage && editable" type="button" class="button primary" @click="emit('add-stage')">Crear primera etapa</button>
    </div>
    <div v-else ref="board" class="board stage-board" :class="{ 'stage-board-grabbing': grabbedStage }"
      @pointermove="moveDrag" @pointerup="endDrag" @pointercancel="cancelDrag" @lostpointercapture="cancelDrag">
      <TransitionGroup name="stage-order">
        <section v-for="(stage, index) in stages" :key="stage.id" :data-stage-id="stage.id" class="board-column"
          :class="{ inactive: !stage.isActive, 'stage-board-grabbed': grabbedStage === stage.id }">
          <header>
            <div>
              <span v-if="canReorder" class="stage-drag-handle" :class="{ 'stage-drag-unavailable': busy }" aria-hidden="true"
                title="Arrastra para cambiar el orden" @pointerdown="startDrag($event, index)">
                <svg viewBox="0 0 16 20" width="16" height="20" fill="currentColor">
                  <circle cx="5" cy="4" r="1.5" /><circle cx="11" cy="4" r="1.5" />
                  <circle cx="5" cy="10" r="1.5" /><circle cx="11" cy="10" r="1.5" />
                  <circle cx="5" cy="16" r="1.5" /><circle cx="11" cy="16" r="1.5" />
                </svg>
              </span>
              <span class="stage-index">{{ String(stage.position).padStart(2, '0') }}</span>
              <h2>{{ stage.name }}</h2>
            </div>
            <span>{{ tasksByStage.get(stage.id)?.length || 0 }}</span>
          </header>
          <p v-if="stage.description">{{ stage.description }}</p>
          <div v-if="canManage" class="stage-actions">
            <button type="button" class="text-button" :disabled="busy || !editable" @click="emit('edit-stage', stage)">Editar</button>
            <button v-if="canReorder" type="button" class="text-button stage-move" :aria-disabled="busy || index === 0"
              :aria-label="`Mover ${stage.name} a la izquierda`" title="Mover a la izquierda"
              @click="requestMove(stage.id, index - 1, $event.currentTarget)">&larr;</button>
            <button v-if="canReorder" type="button" class="text-button stage-move" :aria-disabled="busy || index === stages.length - 1"
              :aria-label="`Mover ${stage.name} a la derecha`" title="Mover a la derecha"
              @click="requestMove(stage.id, index + 1, $event.currentTarget)">&rarr;</button>
            <button type="button" class="text-button" :disabled="busy || !editable" @click="emit('toggle-stage', stage)">{{ stage.isActive ? 'Desactivar' : 'Activar' }}</button>
          </div>
          <button v-if="canManage && editable && stage.isActive" type="button" class="new-task" :disabled="busy" @click="emit('add-task', stage)">+ Nueva tarea</button>
          <div class="task-stack">
            <TaskCard v-for="task in tasksByStage.get(stage.id) || []" :key="task.id" :task="task" />
            <div v-if="!tasksByStage.has(stage.id)" class="column-empty">No hay tareas en esta etapa.</div>
          </div>
        </section>
      </TransitionGroup>
    </div>
  </div>
</template>
