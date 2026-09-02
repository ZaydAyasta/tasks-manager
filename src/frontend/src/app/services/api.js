const BASE_URL = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000').replace(/\/$/, '')

export class ApiError extends Error {
  constructor(status, problem = {}) {
    super(problem.detail || problem.title || 'Ocurrió un error al comunicarse con el servidor.')
    this.name = 'ApiError'; this.status = status; this.problem = problem
  }
}

let tokenProvider = () => null
let unauthorizedHandler = () => {}
export const configureApi = ({ getToken, onUnauthorized }) => { tokenProvider = getToken || tokenProvider; unauthorizedHandler = onUnauthorized || unauthorizedHandler }

export async function api(path, { method = 'GET', body, signal, headers = {} } = {}) {
  const token = tokenProvider()
  let response
  try {
    response = await fetch(`${BASE_URL}${path}`, { method, signal, headers: { Accept: 'application/json', ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}), ...(token ? { Authorization: `Bearer ${token}` } : {}), ...headers }, body: body === undefined ? undefined : JSON.stringify(body) })
  } catch (error) {
    if (error.name === 'AbortError') throw error
    throw new ApiError(0, { title: 'No se pudo conectar con el servidor.' })
  }
  if (response.status === 204) return null
  const contentType = response.headers.get('content-type') || ''
  const data = contentType.includes('json') ? await response.json() : null
  if (!response.ok) {
    const error = new ApiError(response.status, data || {})
    if (response.status === 401) unauthorizedHandler()
    throw error
  }
  return data
}

export async function apiForm(path, formData, { method = 'POST', signal } = {}) {
  const token = tokenProvider(); let response
  try { response = await fetch(`${BASE_URL}${path}`, { method, signal, headers: { Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: formData }) } catch (error) { if (error.name === 'AbortError') throw error; throw new ApiError(0, { title: 'No se pudo conectar con el servidor.' }) }
  const data = (response.headers.get('content-type') || '').includes('json') ? await response.json() : null
  if (!response.ok) { const error = new ApiError(response.status, data || {}); if (response.status === 401) unauthorizedHandler(); throw error }
  return data
}

export async function apiBlob(path, { signal } = {}) {
  const token = tokenProvider(); let response
  try { response = await fetch(`${BASE_URL}${path}`, { signal, headers: { ...(token ? { Authorization: `Bearer ${token}` } : {}) } }) } catch (error) { if (error.name === 'AbortError') throw error; throw new ApiError(0, { title: 'No se pudo conectar con el servidor.' }) }
  if (!response.ok) { const data = (response.headers.get('content-type') || '').includes('json') ? await response.json() : null; const error = new ApiError(response.status, data || {}); if (response.status === 401) unauthorizedHandler(); throw error }
  return response.blob()
}

export const apiMessage = error => error?.status === 409 ? (error.problem?.detail || error.problem?.title || 'La operación entra en conflicto con el estado actual. Actualiza la tarea e inténtalo de nuevo.') : error?.message || 'Ocurrió un error inesperado.'
