import { beforeEach, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
const { auth, tasks, comments, attachments, projects } = vi.hoisted(() => ({
  auth: { isAdmin: false, user: { id: 'member' } },
  tasks: Object.fromEntries(['get', 'subtasks', 'blockers', 'dependencies', 'dependents', 'activity', 'workflow', 'addDependency', 'removeDependency'].map(key => [key, vi.fn()])),
  comments: { list: vi.fn(), create: vi.fn() }, attachments: { list: vi.fn() },
  projects: { members: vi.fn(), stages: vi.fn(), tasks: vi.fn() }
}))
vi.mock('../../app/stores/auth.store', () => ({ useAuthStore: () => auth }))
vi.mock('./tasks.service', () => ({ tasksService: tasks, workflowActions: (status, admin) => status === 'Pending' ? [{ id: 'start', label: 'Iniciar' }] : [] }))
vi.mock('./comments.service', () => ({ commentsService: comments }))
vi.mock('./attachments.service', () => ({ attachmentsService: attachments }))
vi.mock('../projects/projects.service', () => ({ projectsService: projects }))
import TaskDetailView from './TaskDetailView.vue'
const render = () => mount(TaskDetailView, { props: { taskId: 'task' }, global: { stubs: { RouterLink: { template: '<a><slot /></a>' }, BaseModal: true } } })
const button = (view, label) => view.findAll('button').find(item => item.text() === label)
beforeEach(() => {
  vi.clearAllMocks(); auth.isAdmin = false
  tasks.get.mockResolvedValue({ id: 'task', projectId: 'project', title: 'Review evidence', status: 'Pending', priority: 'Medium', version: 7, assignees: [], subtaskProgress: { completed: 0, total: 0 } })
  for (const key of ['subtasks', 'blockers', 'dependencies', 'dependents']) tasks[key].mockResolvedValue([])
  tasks.activity.mockResolvedValue({ items: [], nextCursor: null })
  comments.list.mockResolvedValue({ items: [], nextCursor: null }); attachments.list.mockResolvedValue([])
  projects.members.mockResolvedValue([]); projects.stages.mockResolvedValue([])
  projects.tasks.mockResolvedValue([{ id: 'prerequisite', title: 'Prepare files' }])
  vi.spyOn(window, 'confirm').mockReturnValue(true)
})
it('unassigned collaborator can comment but has no workflow, checklist, blocker or dependency mutations', async () => {
  const view = render(); await flushPromises()
  expect(button(view, 'Iniciar')).toBeUndefined()
  expect(button(view, 'Reportar bloqueo')).toBeUndefined()
  expect(button(view, 'Agregar dependencia')).toBeUndefined()
  expect(view.find('input[aria-label="Nueva subtarea"]').exists()).toBe(false)
  expect(button(view, 'Comentar')).toBeDefined()
})
it('administrator can create dependency with current version and cancel task', async () => {
  auth.isAdmin = true
  const view = render(); await flushPromises()
  await view.get('section:nth-child(4) select').setValue('prerequisite')
  await button(view, 'Agregar dependencia').trigger('submit'); await flushPromises()
  expect(tasks.addDependency).toHaveBeenCalledWith('task', 'prerequisite', 7)
  await button(view, 'Cancelar tarea').trigger('click'); await flushPromises()
  expect(tasks.workflow).toHaveBeenCalledWith('task', 'cancel', 7)
})
it('comment mutation never displays upload progress', async () => {
  let finish
  comments.create.mockReturnValue(new Promise(resolve => { finish = resolve }))
  const view = render(); await flushPromises()
  await view.get('textarea[aria-label="Nuevo comentario"]').setValue('Review update')
  await view.get('.comment-form').trigger('submit')
  expect(view.text()).toContain('Guardando cambios...')
  expect(view.get('.attachments .button').text()).toBe('Subir archivo')
  finish(); await flushPromises()
})
it('loads subsequent comments and activity pages with their respective cursors', async () => {
  tasks.activity.mockResolvedValueOnce({ items: [{ id: 'a1', actor: { fullName: 'Ana' }, activityType: 'TaskCreated', occurredAt: '2026-10-22T12:00:00Z' }], nextCursor: 'activity-next' })
    .mockResolvedValueOnce({ items: [{ id: 'a2', actor: { fullName: 'Ana' }, activityType: 'TaskUpdated', occurredAt: '2026-10-22T11:00:00Z' }], nextCursor: null })
  comments.list.mockResolvedValueOnce({ items: [], nextCursor: 'comments-next' }).mockResolvedValueOnce({ items: [{ id: 'c2', author: { id: 'other', fullName: 'Luis' }, content: 'Second page', createdAt: '2026-10-22T12:00:00Z' }], nextCursor: null })
  const view = render(); await flushPromises()
  await button(view, 'Cargar más actividad').trigger('click'); await flushPromises()
  await button(view, 'Cargar más comentarios').trigger('click'); await flushPromises()
  expect(tasks.activity).toHaveBeenLastCalledWith('task', 'activity-next')
  expect(comments.list).toHaveBeenLastCalledWith('task', 'comments-next')
  expect(view.text()).toContain('editó la tarea'); expect(view.text()).toContain('Second page')
})
