import { describe, expect, it, vi } from 'vitest'
vi.mock('../../app/services/api', () => ({ api: vi.fn(), apiForm: vi.fn(), apiBlob: vi.fn() }))
import { api, apiBlob, apiForm } from '../../app/services/api'
import { commentsService } from './comments.service'
import { attachmentsService } from './attachments.service'
describe('task collaboration services', () => {
  it('sends comments through task endpoints without an author id', async () => { await commentsService.create('task-1', 'Nota'); expect(api).toHaveBeenCalledWith('/api/tasks/task-1/comments', { method: 'POST', body: { content: 'Nota' } }); await commentsService.update('task-1', 'comment-1', 'Editada'); expect(api).toHaveBeenCalledWith('/api/tasks/task-1/comments/comment-1', { method: 'PUT', body: { content: 'Editada' } }) })
  it('uploads multipart and downloads with the authenticated blob client', async () => { const file = new File(['x'], 'nota.txt', { type: 'text/plain' }); await attachmentsService.upload('task-1', file); expect(apiForm).toHaveBeenCalledWith('/api/tasks/task-1/attachments', expect.any(FormData)); await attachmentsService.download('/api/tasks/task-1/attachments/file-1/download'); expect(apiBlob).toHaveBeenCalledWith('/api/tasks/task-1/attachments/file-1/download') })
})
