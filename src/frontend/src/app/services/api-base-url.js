export function resolveApiBaseUrl(value, isProduction) {
  const baseUrl = value?.trim()
  if (!baseUrl) {
    if (isProduction) throw new Error('VITE_API_BASE_URL must be configured for a production build.')
    return 'http://localhost:5000'
  }

  const normalized = baseUrl.replace(/\/$/, '')
  if (!isProduction) return normalized

  let url
  try {
    url = new URL(normalized)
  } catch {
    throw new Error('VITE_API_BASE_URL must be an absolute HTTPS URL for a production build.')
  }

  const hostname = url.hostname.replace(/\.$/, '')
  const isLoopback = hostname === 'localhost' || hostname.endsWith('.localhost') || /^127(?:\.\d{1,3}){3}$/.test(hostname) || hostname === '[::1]'
  if (!/^https:\/\/[^/]/i.test(normalized) || isLoopback || url.username || url.password || /[\s\\"';?#]/.test(normalized)) {
    throw new Error('VITE_API_BASE_URL must be an absolute HTTPS URL without credentials, query, or fragment for a production build.')
  }

  return url.href.replace(/\/$/, '')
}
