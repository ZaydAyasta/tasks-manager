import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'

vi.mock('./dashboard.service', () => ({
  dashboardService: { getDashboard: vi.fn() }
}))

import { dashboardService } from './dashboard.service'
import DashboardView from './DashboardView.vue'

const dashboard = {
  summary: { activeProjects: 2, activeTasks: 8, blockedTasks: 1, inReviewTasks: 2, overdueTasks: 3, criticalTasks: 1 },
  projects: [{ id: 'project-1', name: 'Portal de clientes', status: 'Active', taskProgress: { completed: 3, total: 4 }, blockedCount: 1, inReviewCount: 1, overdueCount: 1 }],
  attention: [{ taskId: 'task-1', title: 'Corregir acceso', projectId: 'project-1', projectName: 'Portal de clientes', status: 'Blocked', priority: 'Critical', dueDate: '2020-01-01T00:00:00Z', assignees: [{ id: 'user-1', fullName: 'Ana Responsable' }] }]
}

const routerStubs = {
  RouterLink: { props: ['to'], template: '<a :data-to="to"><slot /></a>' }
}

describe('Admin dashboard', () => {
  it('renders summary, progress and attention from the aggregate response', async () => {
    dashboardService.getDashboard.mockResolvedValue(dashboard)
    const wrapper = mount(DashboardView, { global: { stubs: routerStubs } })

    await flushPromises()

    expect(wrapper.text()).toContain('2')
    expect(wrapper.text()).toContain('8')
    expect(wrapper.text()).toContain('3 de 4 tareas completadas')
    expect(wrapper.text()).toContain('Corregir acceso')
    expect(wrapper.text()).toContain('Ana Responsable')
    expect(wrapper.get('.dashboard-project-card').attributes('data-to')).toBe('/projects/project-1')
    expect(wrapper.get('.dashboard-attention-item').attributes('data-to')).toBe('/tasks/task-1')
  })

  it('shows loading, error and no-project states', async () => {
    dashboardService.getDashboard.mockImplementation(() => new Promise(() => {}))
    const loading = mount(DashboardView, { global: { stubs: routerStubs } })
    expect(loading.text()).toContain('Cargando información…')

    dashboardService.getDashboard.mockRejectedValue({ message: 'No disponible' })
    const failed = mount(DashboardView, { global: { stubs: routerStubs } })
    await flushPromises()
    expect(failed.text()).toContain('No disponible')

    dashboardService.getDashboard.mockResolvedValue({ ...dashboard, projects: [], attention: [] })
    const empty = mount(DashboardView, { global: { stubs: routerStubs } })
    await flushPromises()
    expect(empty.text()).toContain('No hay proyectos todavía.')
    expect(empty.text()).toContain('Crear proyecto')
  })
})
