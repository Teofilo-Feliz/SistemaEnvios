import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 20000,
  headers: { 'Content-Type': 'application/json' },
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error.response?.status
    const messages = {
      401: 'Tu sesión ha expirado. Inicia sesión nuevamente.',
      403: 'No tienes permisos para realizar esta acción.',
      500: 'Ocurrió un error interno en el servidor.',
    }
    error.userMessage = messages[status] || error.response?.data?.detail || error.response?.data?.message || 'No fue posible completar la operación.'
    if (status === 401) {
      localStorage.removeItem('auth_token')
      window.dispatchEvent(new CustomEvent('auth:unauthorized'))
    }
    return Promise.reject(error)
  },
)

export default api
