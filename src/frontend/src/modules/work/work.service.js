import { api } from '../../app/services/api'
export const workService = { getMyWork: (filters = {}) => api(`/api/me/work${Object.keys(filters).filter(key => filters[key] !== undefined && filters[key] !== null && filters[key] !== '').length ? `?${new URLSearchParams(Object.entries(filters).filter(([, value]) => value !== undefined && value !== null && value !== ''))}` : ''}`) }
