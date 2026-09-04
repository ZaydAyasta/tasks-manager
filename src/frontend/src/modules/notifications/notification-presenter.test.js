import { describe, expect, it } from 'vitest'
import { presentNotification } from './notification-presenter'

describe('notification presenter', () => {
  it('presents a typed notification with its actor and task title', () => {
    expect(presentNotification({
      type: 'TaskAssigned',
      actor: { fullName: 'Ana Pérez' },
      metadata: { taskTitle: 'Revisar propuesta' }
    })).toBe('Ana Pérez te asignó “Revisar propuesta”')
  })

  it('uses safe system text when the actor and type are unknown', () => {
    expect(presentNotification({ type: 'Other', actor: null, metadata: { raw: 'hidden' } }))
      .toBe('El sistema generó una actualización en tu trabajo.')
  })
})
