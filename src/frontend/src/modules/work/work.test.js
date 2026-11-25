import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
vi.mock('../../app/services/api', () => ({ api: vi.fn() }))
vi.mock('./work.service', () => ({ workService: { getMyWork: vi.fn() } }))
vi.mock('../projects/projects.service', () => ({ projectsService: { list: vi.fn(), tasks: vi.fn(), userProjects: vi.fn() } }))
import { workService } from './work.service'; import { projectsService } from '../projects/projects.service'; import MyWorkView from './MyWorkView.vue'; import WorkTaskCard from './WorkTaskCard.vue'
const card = (status, id) => ({ id, title: `${status} task`, status, priority: status === 'Blocked' ? 'Critical' : 'Medium', project: { id: 'p', name: 'Proyecto Atlas' }, stage: { id: 's', name: 'Construcción' }, subtaskProgress: { completed: 1, total: 3 }, pendingDependencyCount: status === 'Pending' ? 2 : 0, activeBlockerCount: status === 'Blocked' ? 1 : 0 })
const stubs = { RouterLink: { template: '<a><slot /></a>' } }
describe('My Work', () => {
  it('uses the dedicated work feed and loads only project options for filtering', async () => { vi.clearAllMocks(); workService.getMyWork.mockResolvedValue({ items: [] }); projectsService.list.mockResolvedValue([]); mount(MyWorkView, { global: { stubs } }); await flushPromises(); expect(workService.getMyWork).toHaveBeenCalledOnce(); expect(projectsService.list).toHaveBeenCalledOnce(); expect(projectsService.tasks).not.toHaveBeenCalled(); expect(projectsService.userProjects).not.toHaveBeenCalled() })
  it('appends cursor pages without duplicates and resets results when filters change', async () => {
    vi.clearAllMocks()
    projectsService.list.mockResolvedValue([{ id: 'p', name: 'Proyecto Atlas' }])
    workService.getMyWork.mockResolvedValueOnce({ items: [card('Pending', 'one')], nextCursor: 'next' })
      .mockResolvedValueOnce({ items: [card('Pending', 'one'), card('Pending', 'two')], nextCursor: null })
      .mockResolvedValueOnce({ items: [card('Completed', 'done')], nextCursor: null })
    const view = mount(MyWorkView, { global: { stubs } }); await flushPromises()
    await view.findAll('select')[0].setValue('Completed'); await view.findAll('select')[1].setValue('High'); await view.findAll('select')[2].setValue('p')
    await view.findAll('button').find(item => item.text() === 'Cargar más tareas').trigger('click'); await flushPromises()
    expect(workService.getMyWork).toHaveBeenNthCalledWith(2, { status: '', priority: '', projectId: '', cursor: 'next' })
    expect(view.findAll('.work-task-card')).toHaveLength(2)
    await view.get('form').trigger('submit'); await flushPromises()
    expect(workService.getMyWork).toHaveBeenLastCalledWith({ status: 'Completed', priority: 'High', projectId: 'p' })
    expect(view.findAll('.work-task-card')).toHaveLength(1)
    expect(view.text()).toContain('Completadas')
    expect(view.text()).not.toContain('Cargar más tareas')
  })
  it('groups blocked work before the other statuses and handles empty state', async () => { workService.getMyWork.mockResolvedValue({ items: [card('Pending', 'p'), card('InReview', 'r'), card('InProgress', 'i'), card('Blocked', 'b')] }); const wrapper = mount(MyWorkView, { global: { stubs } }); await Promise.resolve(); await wrapper.vm.$nextTick(); const text = wrapper.text(); expect(text.indexOf('Bloqueadas')).toBeLessThan(text.indexOf('En progreso')); expect(text.indexOf('En progreso')).toBeLessThan(text.indexOf('Pendientes')); expect(text.indexOf('Pendientes')).toBeLessThan(text.indexOf('En revisión')); workService.getMyWork.mockResolvedValue({ items: [] }); const empty = mount(MyWorkView, { global: { stubs } }); await Promise.resolve(); await empty.vm.$nextTick(); expect(empty.text()).toContain('No tienes tareas asignadas') })
  it('renders work context, progress, dependencies and blockers', () => { const wrapper = mount(WorkTaskCard, { props: { task: { ...card('Blocked', 'b'), pendingDependencyCount: 2 } }, global: { stubs } }); expect(wrapper.text()).toContain('Proyecto Atlas'); expect(wrapper.text()).toContain('Construcción'); expect(wrapper.text()).toContain('1/3 subtareas'); expect(wrapper.text()).toContain('Esperando 2 dependencias'); expect(wrapper.text()).toContain('1 bloqueo') })
})
