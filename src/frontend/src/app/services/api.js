const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim()

export function resolveApiBaseUrl(baseUrl, isProduction) {
  if (!baseUrl) {
    if (isProduction) {
      throw new Error('VITE_API_BASE_URL must be configured for a production build.')
    }

    return 'http://localhost:5000'
  }

  const normalizedBaseUrl = baseUrl.replace(/\/$/, '')
  if (!isProduction) {
    return normalizedBaseUrl
  }

  let url
  try {
    url = new URL(normalizedBaseUrl)
  } catch {
    throw new Error('VITE_API_BASE_URL must be an absolute HTTPS URL for a production build.')
  }

  const isLoopbackHost = ['localhost', '127.0.0.1', '::1', '[::1]'].includes(url.hostname)
  if (url.protocol !== 'https:' || isLoopbackHost || url.username || url.password || url.search || url.hash) {
    throw new Error('VITE_API_BASE_URL must be an absolute HTTPS URL without credentials, query, or fragment for a production build.')
  }

  return normalizedBaseUrl
}

const BASE_URL = resolveApiBaseUrl(configuredBaseUrl, import.meta.env.PROD)

export class ApiError extends Error {
  constructor(status, problem = {}) {
    super(problem.detail || problem.title || 'Ocurrió un error al comunicarse con el servidor.')
    this.name = 'ApiError'; this.status = status; this.problem = problem
  }
}

let tokenProvider = () => null
let csrfTokenProvider = () => null
let unauthorizedHandler = () => {}

export const configureApi = ({ getToken, getCsrfToken, onUnauthorized }) => {
  tokenProvider = getToken || tokenProvider
  csrfTokenProvider = getCsrfToken || csrfTokenProvider
  unauthorizedHandler = onUnauthorized || unauthorizedHandler
}

const isSafeMethod = method => ['GET', 'HEAD', 'OPTIONS'].includes(method.toUpperCase())
const authenticationHeaders = method => {
  const token = tokenProvider()
  const csrfToken = csrfTokenProvider()
  return {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(!isSafeMethod(method) && csrfToken ? { 'X-Nakama-Csrf': csrfToken } : {})
  }
}

export async function api(path, { method = 'GET', body, signal, headers = {} } = {}) {
  let response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      credentials: 'include',
      signal,
      headers: {
        Accept: 'application/json',
        ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}),
        ...authenticationHeaders(method),
        ...headers
      },
      body: body === undefined ? undefined : JSON.stringify(body)
    })
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
  let response

  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      credentials: 'include',
      signal,
      headers: {
        Accept: 'application/json',
        ...authenticationHeaders(method)
      },
      body: formData
    })
  } catch (error) {
    if (error.name === 'AbortError') throw error
    throw new ApiError(0, { title: 'No se pudo conectar con el servidor.' })
  }

  const data = (response.headers.get('content-type') || '').includes('json') ? await response.json() : null
  if (!response.ok) {
    const error = new ApiError(response.status, data || {})
    if (response.status === 401) unauthorizedHandler()
    throw error
  }

  return data
}

export async function apiBlob(path, { signal } = {}) {
  let response

  try {
    response = await fetch(`${BASE_URL}${path}`, {
      credentials: 'include',
      signal,
      headers: authenticationHeaders('GET')
    })
  } catch (error) {
    if (error.name === 'AbortError') throw error
    throw new ApiError(0, { title: 'No se pudo conectar con el servidor.' })
  }

  if (!response.ok) {
    const data = (response.headers.get('content-type') || '').includes('json') ? await response.json() : null
    const error = new ApiError(response.status, data || {})
    if (response.status === 401) unauthorizedHandler()
    throw error
  }

  return response.blob()
}

export const apiMessage = error => error?.status === 409
  ? (error.problem?.detail || error.problem?.title || 'La operación entra en conflicto con el estado actual. Actualiza la tarea e inténtalo de nuevo.')
  : error?.message || 'Ocurrió un error inesperado.'
