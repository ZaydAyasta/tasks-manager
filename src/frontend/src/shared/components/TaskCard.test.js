import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import TaskCard from './TaskCard.vue'
describe('TaskCard', () => {
  it('shows progress, dependency flag and blocked status', () => { const wrapper = mount(TaskCard, { props: { task: { id: 'task-1', title: 'Resolver incidencia', status: 'Blocked', priority: 'Critical', pendingDependencyCount: 2, subtaskProgress: { completed: 1, total: 3 }, assignees: [{ id: 'u1', fullName: 'Ana' }] } }, global: { stubs: { RouterLink: { template: '<a><slot /></a>' } } } }); expect(wrapper.text()).toContain('1/3 subtareas'); expect(wrapper.text()).toContain('2 dep.'); expect(wrapper.text()).toContain('Bloqueada'); expect(wrapper.text()).toContain('Crítica') })
})
