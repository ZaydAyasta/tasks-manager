import { flushPromises, mount } from '@vue/test-utils'
import { expect, it, vi } from 'vitest'
import ProjectActivity from './ProjectActivity.vue'
import { projectsService } from './projects.service'

vi.mock('./projects.service', () => ({ projectsService: { activity: vi.fn() } }))

it('keeps visible project changes when a page fails and retries without duplicates', async () => {
  const created = { id: '1', actor: { fullName: 'Ana' }, activityType: 'ProjectCreated', occurredAt: '2026-11-25T14:00:00Z' }
  const completed = { ...created, id: '2', activityType: 'TaskCompleted', taskId: 'task-2' }
  projectsService.activity.mockResolvedValueOnce({ items: [created], nextCursor: 'older' })
    .mockRejectedValueOnce(new Error('Conexión interrumpida'))
    .mockResolvedValueOnce({ items: [created, completed], nextCursor: null })

  const view = mount(ProjectActivity, { props: { projectId: 'project-1' }, global: { stubs: { RouterLink: true } } })
  await flushPromises()
  await view.get('button').trigger('click')
  await flushPromises()
  expect(view.get('[role="alert"]').text()).toBe('Conexión interrumpida')
  expect(view.findAll('li')).toHaveLength(1)

  await view.get('button').trigger('click')
  await flushPromises()
  expect(projectsService.activity).toHaveBeenLastCalledWith('project-1', 'older')
  expect(view.findAll('li')).toHaveLength(2)
  expect(view.get('router-link-stub').attributes('to')).toBe('/tasks/task-2')
  expect(view.find('button').exists()).toBe(false)
})
