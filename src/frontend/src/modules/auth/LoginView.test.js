import { describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'

const { login, replace } = vi.hoisted(() => ({
  login: vi.fn(),
  replace: vi.fn()
}))

vi.mock('vue-router', () => ({
  useRoute: () => ({ query: {} }),
  useRouter: () => ({ replace })
}))

vi.mock('../../app/stores/auth.store', () => ({
  useAuthStore: () => ({ login })
}))

import LoginView from './LoginView.vue'

async function submit(role) {
  login.mockResolvedValue({ role })
  const wrapper = mount(LoginView)
  await wrapper.get('input[type="email"]').setValue('user@nakama.test')
  await wrapper.get('input[type="password"]').setValue('Password1')
  await wrapper.get('form').trigger('submit.prevent')
  await flushPromises()
}

describe('login redirects', () => {
  it('sends an Admin to Dashboard', async () => {
    await submit('Admin')

    expect(replace).toHaveBeenCalledWith('/dashboard')
  })

  it('sends a Collaborator to My Work', async () => {
    await submit('Collaborator')

    expect(replace).toHaveBeenCalledWith('/my-work')
  })

  it('shows a connection error when the API is unavailable', async () => {
    login.mockRejectedValue({ status: 0, message: 'No se pudo conectar con el servidor.' })
    const wrapper = mount(LoginView)

    await wrapper.get('input[type="email"]').setValue('user@nakama.test')
    await wrapper.get('input[type="password"]').setValue('Password1')
    await wrapper.get('form').trigger('submit.prevent')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toBe('No se pudo conectar con el servidor.')
  })
})
