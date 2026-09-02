import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
vi.mock('../../app/services/api', () => ({ api: vi.fn(), configureApi: vi.fn() }))
vi.mock('./projects.service', () => ({ projectsService: { list: vi.fn(), userProjects: vi.fn(), create: vi.fn() } }))
import { projectsService } from './projects.service'; import { useAuthStore } from '../../app/stores/auth.store'; import ProjectsView from './ProjectsView.vue'; import ProjectMembersPanel from './ProjectMembersPanel.vue'; import MemberSelector from './MemberSelector.vue'; import { projectPayload, runAdminMutation } from '../../app/services/admin-ui'
const stubs = { RouterLink: { template: '<a><slot /></a>' }, BaseModal: { template: '<div><slot /></div>' } }
describe('admin project management', () => {
  beforeEach(() => { setActivePinia(createPinia()); vi.clearAllMocks(); projectsService.list.mockResolvedValue([]) })
  it('shows the create-project CTA to an Admin', async () => { const auth = useAuthStore(); auth.user = { role: 'Admin' }; auth.accessToken = 'token'; const wrapper = mount(ProjectsView, { global: { stubs } }); await Promise.resolve(); expect(wrapper.text()).toContain('+ Nuevo proyecto') })
  it('does not show member management actions to a Collaborator', () => { const wrapper = mount(ProjectMembersPanel, { props: { members: [{ userId: '1', fullName: 'Ana', email: 'ana@nakama.test', role: 'Member' }], users: [], canManage: false } }); expect(wrapper.text()).not.toContain('Agregar'); expect(wrapper.text()).not.toContain('Quitar') })
  it('filters users that are already project members', () => { const wrapper = mount(MemberSelector, { props: { users: [{ id: 'a', fullName: 'Ana', email: 'a@test', isActive: true }, { id: 'b', fullName: 'Bruno', email: 'b@test', isActive: true }], members: [{ userId: 'a' }] } }); expect(wrapper.text()).not.toContain('Ana'); expect(wrapper.text()).toContain('Bruno') })
  it('passes the normalized create-project payload to the service', async () => { const payload = projectPayload({ name: ' Plataforma ', description: ' Trabajo ', startDate: '2026-01-01', endDate: '' }); await projectsService.create(payload); expect(projectsService.create).toHaveBeenCalledWith({ name: 'Plataforma', description: 'Trabajo', startDate: '2026-01-01', endDate: null }) })
  it('reports and refreshes after a version conflict', async () => { const refresh = vi.fn(); const setError = vi.fn(); await runAdminMutation(() => Promise.reject({ status: 409, problem: { type: 'https://nakama/errors/project-version-conflict' } }), refresh, setError); expect(setError).toHaveBeenCalledWith('El proyecto cambió mientras lo editabas. Recargamos la información.'); expect(refresh).toHaveBeenCalledTimes(1) })
})
