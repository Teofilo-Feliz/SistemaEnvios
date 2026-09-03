import api from './api'

export const equipoService = {
  // Paginado y filtrado en el servidor: el inventario son 34 filiales por todos sus equipos.
  list: (params) => api.get('/equipos', { params }),
  get: (id) => api.get(`/equipos/${id}`),
  create: (payload) => api.post('/equipos', payload),
  update: (id, payload) => api.put(`/equipos/${id}`, payload),
}
