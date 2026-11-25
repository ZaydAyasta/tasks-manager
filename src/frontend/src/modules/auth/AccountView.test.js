import { beforeEach, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
const { auth, replace } = vi.hoisted(() => ({ auth: { user: { fullName: 'Ana', email: 'ana@trello.local', mustChangePassword: true }, isAdmin: false, changePassword: vi.fn() }, replace: vi.fn() }))
vi.mock('../../app/stores/auth.store', () => ({ useAuthStore: () => auth }))
vi.mock('vue-router', () => ({ useRouter: () => ({ replace }) }))
import AccountView from './AccountView.vue'
beforeEach(() => vi.clearAllMocks())
it('rejects mismatched confirmation without sending credentials', async () => {
  const view = mount(AccountView)
  await view.get('input[autocomplete="current-password"]').setValue('TemporaryPassword1')
  await view.findAll('input[autocomplete="new-password"]')[0].setValue('NewPersonalPassword1')
  await view.findAll('input[autocomplete="new-password"]')[1].setValue('different-password')
  await view.get('form').trigger('submit')
  expect(auth.changePassword).not.toHaveBeenCalled()
  expect(view.get('[role="alert"]').text()).toBe('Las contraseñas nuevas no coinciden.')
})
it('changes temporary password, clears form, and continues to personal work', async () => {
  auth.changePassword.mockResolvedValue({ mustChangePassword: false })
  const view = mount(AccountView)
  await view.get('input[autocomplete="current-password"]').setValue('TemporaryPassword1')
  for (const field of view.findAll('input[autocomplete="new-password"]')) await field.setValue('NewPersonalPassword1')
  await view.get('form').trigger('submit'); await flushPromises()
  expect(auth.changePassword).toHaveBeenCalledWith({ currentPassword: 'TemporaryPassword1', newPassword: 'NewPersonalPassword1' })
  expect(replace).toHaveBeenCalledWith('/my-work')
  expect(view.findAll('input').every(field => field.element.value === '')).toBe(true)
})
