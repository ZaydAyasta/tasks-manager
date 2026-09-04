const messages = {
  TaskAssigned: '{actor} te asignó “{taskTitle}”',
  TaskUnassigned: '{actor} te quitó de “{taskTitle}”',
  TaskSubmittedForReview: '{actor} envió “{taskTitle}” a revisión',
  TaskChangesRequested: '{actor} solicitó cambios en “{taskTitle}”',
  TaskCompleted: '{actor} completó “{taskTitle}”',
  BlockerReported: '{actor} reportó un bloqueo en “{taskTitle}”',
  BlockerResolved: '{actor} resolvió un bloqueo en “{taskTitle}”',
  CommentAdded: '{actor} comentó en “{taskTitle}”'
}

const SYSTEM_ACTOR = 'El sistema'
const FALLBACK_TASK = 'una tarea'

export function presentNotification(notification) {
  const actor = notification.actor?.fullName || SYSTEM_ACTOR
  const taskTitle = notification.metadata?.taskTitle || FALLBACK_TASK
  const template = messages[notification.type]

  if (!template) return `${actor} generó una actualización en tu trabajo.`

  return template
    .replace('{actor}', actor)
    .replace('{taskTitle}', taskTitle)
}

export const notificationPreferenceLabels = {
  TaskAssigned: 'Asignaciones',
  TaskUnassigned: 'Desasignaciones',
  TaskSubmittedForReview: 'Envíos a revisión',
  TaskChangesRequested: 'Cambios solicitados',
  TaskCompleted: 'Tareas completadas',
  BlockerReported: 'Bloqueos reportados',
  BlockerResolved: 'Bloqueos resueltos',
  CommentAdded: 'Comentarios'
}
