import { describe, expect, it, vi } from 'vitest'

vi.mock('../../app/services/api', () => ({ api: vi.fn() }))

import { api } from '../../app/services/api'
import { dashboardService } from './dashboard.service'

describe('dashboard service', () => {
  it('uses the dedicated aggregate endpoint', async () => {
    await dashboardService.getDashboard()

    expect(api).toHaveBeenCalledWith('/api/admin/dashboard')
  })
})
