import { api } from '../../app/services/api'

export const dashboardService = {
  getDashboard: () => api('/api/admin/dashboard')
}
