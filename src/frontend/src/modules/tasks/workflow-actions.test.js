import { describe, expect, it } from 'vitest'
import { workflowActions } from './tasks.service'
describe('collaborator workflow visibility', () => { it('shows Start and Submit Review but not Admin review controls', () => { expect(workflowActions('Pending', false).map(action => action.id)).toEqual(['start']); expect(workflowActions('InProgress', false).map(action => action.id)).toEqual(['submit-review']); expect(workflowActions('InReview', false)).toEqual([]); expect(workflowActions('InReview', true).map(action => action.id)).toEqual(['request-changes', 'complete']) }) })
