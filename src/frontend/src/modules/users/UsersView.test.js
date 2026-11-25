import { beforeEach, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
const { service } = vi.hoisted(() => ({ service: { list: vi.fn(), create: vi.fn(), setActive: vi.fn(), changeRole: vi.fn(), resetPassword: vi.fn() } }))
vi.mock('./users.service', () => ({ userAdminService: service }))
vi.mock('../../app/stores/auth.store', () => ({ useAuthStore: () => ({ user: { id: 'admin' } }) }))
import UsersView from './UsersView.vue'
const render = () => mount(UsersView, { global: { stubs: { BaseModal: { props: ['modelValue'], template: '<div v-if="modelValue"><slot /></div>' } } } })
const button = (view, text) => view.findAll('button').find(item => item.text() === text)
beforeEach(() => {
  vi.clearAllMocks()
  service.list.mockResolvedValue([
    { id: 'admin', fullName: 'Admin', email: 'admin@trello.local', role: 'Admin', isActive: true },
    { id: 'ana', fullName: 'Ana', email: 'ana@trello.local', role: 'Collaborator', isActive: true }
  ])
})
it('creates local user with mandatory first password change and clears the credential form', async () => {
  const view = render(); await flushPromises(); await button(view, 'Crear usuario').trigger('click')
  const form = view.get('form.admin-form'); const fields = form.findAll('input')
  await fields[0].setValue('Ana Nueva'); await fields[1].setValue('ana.nueva'); await fields[2].setValue('TemporaryPassword123')
  service.create.mockResolvedValue({ id: 'new' })
  await form.trigger('submit'); await flushPromises()
  expect(service.create).toHaveBeenCalledWith({ fullName: 'Ana Nueva', email: 'ana.nueva', role: 'Collaborator', password: 'TemporaryPassword123', mustChangePassword: true })
  expect(view.find('form.admin-form').exists()).toBe(false)
  await button(view, 'Crear usuario').trigger('click')
  expect(view.get('input[type="password"]').element.value).toBe('')
})
it('filters roster and prevents changing the signed-in administrator through the UI', async () => {
  const view = render(); await flushPromises()
  expect(view.get('select[aria-label="Rol de Admin"]').attributes('disabled')).toBeDefined()
  await view.get('input[type="search"]').setValue('ana')
  expect(view.findAll('tbody tr')).toHaveLength(1)
  expect(view.get('tbody').text()).toContain('ana@trello.local')
})
it('resets access through admin API and displays server validation errors', async () => {
  const view = render(); await flushPromises()
  await button(view, 'Restablecer contraseña').trigger('click')
  await view.get('input[type="password"]').setValue('TemporaryPassword123')
  service.resetPassword.mockRejectedValue({ message: 'La cuenta cambió.' })
  await view.get('form.admin-form').trigger('submit'); await flushPromises()
  expect(service.resetPassword).toHaveBeenCalledWith('ana', 'TemporaryPassword123')
  expect(view.text()).toContain('La cuenta cambió.')
  expect(view.get('form.admin-form').exists()).toBe(true)
})
it('activates, deactivates, and changes the role of another user', async () => {
  vi.spyOn(window, 'confirm').mockReturnValue(true)
  const view = render(); await flushPromises()
  await view.get('select[aria-label="Rol de Ana"]').setValue('Admin'); await flushPromises()
  expect(service.changeRole).toHaveBeenCalledWith('ana', 'Admin')
  await button(view, 'Desactivar').trigger('click'); await flushPromises()
  expect(service.setActive).toHaveBeenCalledWith('ana', false)
  service.list.mockResolvedValue([{ id: 'ana', fullName: 'Ana', email: 'ana@trello.local', role: 'Collaborator', isActive: false }])
  const inactiveView = render(); await flushPromises()
  await button(inactiveView, 'Activar').trigger('click'); await flushPromises()
  expect(service.setActive).toHaveBeenLastCalledWith('ana', true)
})
