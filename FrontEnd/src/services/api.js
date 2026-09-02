import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 20000,
  headers: { 'Content-Type': 'application/json' },
})

// El token se pide al store, que renueva si está vencido. Leerlo directo de localStorage
// mandaba tokens expirados y provocaba un 401 evitable en cada primera petición.
let obtenerToken = async () => localStorage.getItem('auth_token')
export function registrarProveedorDeToken(proveedor) {
  obtenerToken = proveedor
}

api.interceptors.request.use(async (config) => {
  const token = await obtenerToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const status = error.response?.status
    const messages = {
      401: 'Tu sesión ha expirado. Inicia sesión nuevamente.',
      403: 'No tienes permisos para realizar esta acción.',
      500: 'Ocurrió un error interno en el servidor.',
    }
    error.userMessage = messages[status] || error.response?.data?.detail || error.response?.data?.message || 'No fue posible completar la operación.'
    // Un 401 puede ser solo un token recién vencido. Se renueva y se reintenta una vez;
    // antes la petición se perdía y el usuario veía un error en una acción que iba a funcionar.
    if (status === 401 && error.config && !error.config._reintentado) {
      error.config._reintentado = true
      const renovado = await new Promise((resolve) => {
        window.dispatchEvent(new CustomEvent('auth:unauthorized', { detail: { resolve } }))
        // Si nadie atiende el evento no se queda colgado esperando.
        setTimeout(() => resolve(null), 8000)
      })
      if (renovado) {
        error.config.headers.Authorization = `Bearer ${renovado}`
        return api.request(error.config)
      }
    }
    if (status === 401) localStorage.removeItem('auth_token')
    return Promise.reject(error)
  },
)

export default api
