import { api } from '../../app/services/api'
export const usersService = { list: () => api('/api/users') }
