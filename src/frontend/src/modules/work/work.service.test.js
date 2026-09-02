import { describe, expect, it, vi } from 'vitest'
vi.mock('../../app/services/api', () => ({ api: vi.fn() }))
import { api } from '../../app/services/api'
import { workService } from './work.service'
describe('work service', () => { it('requests the real aggregated endpoint', async () => { await workService.getMyWork({ status: 'Blocked' }); expect(api).toHaveBeenCalledWith('/api/me/work?status=Blocked') }) })
