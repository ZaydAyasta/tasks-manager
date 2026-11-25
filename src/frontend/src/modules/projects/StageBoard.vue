<script setup>
import { computed, nextTick, onBeforeUnmount, ref, shallowRef, watch } from 'vue'
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
const dragPreview = shallowRef(null)
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
const settleAnimationId = 'stage-settle'

function columns() {
  return Array.from(board.value.children).filter(column => column.dataset.stageId)
}

function startDrag(event, index) {
  if (!canReorder.value || props.busy || drag || grabbedStage.value || !event.isPrimary || event.button !== 0) return

  const handle = event.currentTarget
  const column = handle.closest('[data-stage-id]')
  const items = columns()
  handle.setPointerCapture(event.pointerId)
  drag = {
    pointerId: event.pointerId, handle, column, index,
    items, siblings: items.filter(item => item !== column),
    startX: event.clientX, startScroll: board.value.scrollLeft, x: event.clientX, time: null, target: index,
    pickupOffset: 0
  }
  cancellation.value = ''
  window.addEventListener('keydown', cancelWithEscape)
}

function moveDrag(event) {
  if (drag?.pointerId !== event.pointerId) return
  drag.x = event.clientX
  if (!grabbedStage.value) {
    if (Math.abs(drag.x - drag.startX) < 6) return
    const { column } = drag
    const current = column.getBoundingClientRect().left
    cancelSettle(column)
    drag.pickupOffset = current - column.getBoundingClientRect().left
    board.value.style.setProperty('--stage-source-x', `${column.offsetLeft}px`)
    board.value.style.setProperty('--stage-source-width', `${column.offsetWidth}px`)
    positionDraggedColumn()
    grabbedStage.value = column.dataset.stageId
  }
  if (!frame) frame = requestAnimationFrame(animateDrag)
}

function updateTarget() {
  const list = board.value
  const left = list.getBoundingClientRect().left + list.clientLeft
  const x = list.scrollLeft + Math.max(0, Math.min(list.clientWidth, drag.x - left))
  const { siblings } = drag
  const next = siblings.findIndex(column => x < column.offsetLeft + column.offsetWidth / 2)
  drag.target = next === -1 ? siblings.length : next
  if (drag.target === drag.index) {
    dragPreview.value = null
    return
  }

  const marker = previewDrop()
  const visibleStart = list.scrollLeft
  const visibleEnd = visibleStart + list.clientWidth
  list.style.setProperty('--stage-drop-x', `${Math.max(visibleStart + 4, Math.min(visibleEnd - 4, marker))}px`)
  list.style.setProperty('--stage-drop-label-x', `${Math.max(visibleStart + 62, Math.min(visibleEnd - 62, marker))}px`)
}

function previewDrop() {
  const { target, index: origin, items } = drag
  const towardOrigin = Math.sign(origin - target)
  const destination = items[target]
  const nudge = (items[target + towardOrigin].offsetLeft - destination.offsetLeft) * 0.42

  if (dragPreview.value?.target !== target || dragPreview.value.nudge !== nudge) {
    const offsets = new Map()
    for (let index = target; index !== origin; index += towardOrigin) {
      const column = items[index]
      const offset = index === target ? nudge : items[index + towardOrigin].offsetLeft - column.offsetLeft
      offsets.set(column.dataset.stageId, `${offset}px`)
    }
    dragPreview.value = { target, nudge, offsets, coversOrigin: Math.abs(origin - target) > 1 }
  }

  const outside = items[target - towardOrigin]
  const goingLeft = towardOrigin > 0
  const destinationEdge = destination.offsetLeft + (goingLeft ? 0 : destination.offsetWidth)
  const outsideEdge = outside
    ? outside.offsetLeft + (goingLeft ? outside.offsetWidth : 0)
    : destinationEdge - towardOrigin * 12
  return (outsideEdge + destinationEdge + nudge) / 2
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
  const lastColumn = drag.items.at(-1)
  // Limit auto-scroll to the real columns even if the lifted column paints beyond them.
  const contentEnd = lastColumn.offsetLeft + lastColumn.offsetWidth + 16
  const maxScroll = Math.max(0, contentEnd - list.clientWidth)
  list.scrollLeft = Math.max(0, Math.min(maxScroll, list.scrollLeft + velocity * elapsed * 0.75))
  positionDraggedColumn()
  updateTarget()
  frame = requestAnimationFrame(animateDrag)
}

function positionDraggedColumn() {
  const offset = drag.x - drag.startX + board.value.scrollLeft - drag.startScroll + drag.pickupOffset
  drag.column.style.setProperty('--stage-drag-x', `${offset}px`)
}

function releasePointer() {
  if (!drag) return
  const { handle, pointerId } = drag
  drag = null
  cancelAnimationFrame(frame)
  frame = 0
  window.removeEventListener('keydown', cancelWithEscape)
  if (handle.hasPointerCapture(pointerId)) handle.releasePointerCapture(pointerId)
}

function cancelSettle(column) {
  column.getAnimations?.().find(animation => animation.id === settleAnimationId)?.cancel()
}

async function settleStage({ stageId, button }) {
  const list = board.value
  const items = columns()
  const moving = items.find(column => column.dataset.stageId === stageId)
  const canAnimate = typeof moving?.animate === 'function' && !window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
  // Exclude the temporary rotation from the starting rects, keeping the horizontal preview.
  list.classList.remove('stage-board-grabbing')
  const previous = canAnimate ? items.map(column => [column, column.getBoundingClientRect()]) : []
  const scrollBefore = list.scrollLeft
  grabbedStage.value = null
  dragPreview.value = null
  moving?.style.removeProperty('--stage-drag-x')
  await nextTick()
  if (board.value !== list || !moving?.isConnected) return

  previous.forEach(([column]) => cancelSettle(column))
  const start = moving.offsetLeft - 8
  const end = moving.offsetLeft + moving.offsetWidth + 8 - list.clientWidth
  list.scrollLeft = Math.min(start, Math.max(end, list.scrollLeft))
  const scrollDelta = scrollBefore - list.scrollLeft
  // Measure the final layout before starting any column animation.
  const offsets = previous.filter(([column]) => column.isConnected).map(([column, from]) => {
    const to = column.getBoundingClientRect()
    const x = from.left - to.left + (column === moving ? 0 : scrollDelta)
    return [column, x, from.top - to.top, from.width / to.width]
  })
  for (const [column, x, y, scale] of offsets) {
    if (Math.abs(x) < 1 && Math.abs(y) < 1 && Math.abs(scale - 1) < 0.005) continue
    const zIndex = column === moving ? 2 : 1
    column.animate([
      { translate: `${x}px ${y}px`, scale, zIndex },
      { translate: '0 0', scale: 1, zIndex }
    ], { id: settleAnimationId, duration: 290, easing: 'cubic-bezier(.23, 1, .32, 1)' })
  }
  if (button?.isConnected && document.activeElement === document.body) button.focus({ preventScroll: true })
}

function returnDraggedStage() {
  const stageId = grabbedStage.value
  releasePointer()
  if (stageId) settleStage({ stageId })
}

function endDrag(event) {
  if (drag?.pointerId !== event.pointerId) return
  const { top, bottom } = board.value.getBoundingClientRect()
  if (!grabbedStage.value || !canReorder.value || props.busy || event.clientY < top || event.clientY > bottom) {
    returnDraggedStage()
    return
  }

  drag.x = event.clientX
  positionDraggedColumn()
  updateTarget()
  const { index, target, column } = drag
  if (target === index) {
    returnDraggedStage()
    return
  }
  releasePointer()
  requestMove(column.dataset.stageId, target)
}

function cancelDrag(event) {
  if (drag?.pointerId === event.pointerId) returnDraggedStage()
}

function cancelWithEscape(event) {
  if (event.key !== 'Escape') return
  if (grabbedStage.value) cancellation.value = 'Se canceló el cambio de orden.'
  returnDraggedStage()
}

function requestMove(stageId, target, button = null) {
  if (!canReorder.value || props.busy || drag || target < 0 || target >= props.stages.length) return
  pendingMove = { stageId, button }
  cancellation.value = ''
  emit('reorder', stageId, target)
}

watch(() => props.stages.map(stage => stage.id).join(','), () => {
  if (!pendingMove || !board.value) return
  const move = pendingMove
  pendingMove = null
  // Capture the released position before Vue patches the saved order into the board.
  settleStage(move)
})

watch(() => props.busy, value => {
  if (value || !pendingMove) return
  pendingMove = null
  if (grabbedStage.value) settleStage({ stageId: grabbedStage.value })
}, { flush: 'post' })

onBeforeUnmount(releasePointer)
</script>

<template>
  <div>
    <div v-if="canManage && editable" class="board-actions stage-board-actions">
      <p v-if="canReorder" class="muted">Arrastra una etapa desde los seis puntos o usa las flechas para cambiar su orden.</p>
      <button type="button" class="button secondary" :disabled="busy" @click="emit('add-stage')">+ Agregar etapa</button>
    </div>
    <div class="stage-order-messages" :class="{ 'stage-order-messages-reserved': canReorder }">
      <p class="stage-order-feedback muted" role="status">{{ cancellation || orderFeedback }}</p>
      <p v-if="orderError" class="stage-order-feedback form-error" role="alert">{{ orderError }}</p>
    </div>

    <div v-if="!stages.length" class="state-card">
      <b>Este proyecto todavía no tiene etapas.</b>
      <span>Organiza el flujo de trabajo antes de añadir tareas.</span>
      <button v-if="canManage && editable" type="button" class="button primary" @click="emit('add-stage')">Crear primera etapa</button>
    </div>
    <div v-else ref="board" class="board stage-board"
      :class="{ 'stage-board-reorderable': canReorder, 'stage-board-previewing': grabbedStage, 'stage-board-grabbing': grabbedStage && !busy }"
      @pointermove="moveDrag" @pointerup="endDrag" @pointercancel="cancelDrag" @lostpointercapture="cancelDrag">
      <div v-if="grabbedStage && !dragPreview?.coversOrigin" class="stage-source-slot" aria-hidden="true">Origen</div>
      <div v-if="dragPreview" class="stage-drop-marker" aria-hidden="true">
        <span>Posición {{ dragPreview.target + 1 }}</span>
      </div>
      <section v-for="(stage, index) in stages" :key="stage.id" :data-stage-id="stage.id" class="board-column"
        :class="{ inactive: !stage.isActive, 'stage-board-grabbed': grabbedStage === stage.id }"
        :style="{ '--stage-preview-x': dragPreview?.offsets.get(stage.id) }">
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
    </div>
  </div>
</template>
