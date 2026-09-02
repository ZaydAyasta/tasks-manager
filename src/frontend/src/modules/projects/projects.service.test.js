import { describe, expect, it, vi } from 'vitest'
vi.mock('../../app/services/api', () => ({ api: vi.fn() }))
import { api } from '../../app/services/api'
import { projectsService } from './projects.service'
describe('projects service', () => { it('posts project creation to the real endpoint', async () => { const body = { name: 'Plataforma', description: null, startDate: null, endDate: null }; await projectsService.create(body); expect(api).toHaveBeenCalledWith('/api/projects', { method: 'POST', body }) }) })
