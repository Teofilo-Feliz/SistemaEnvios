import api from './api'

export const equipoService = {
  list: (params) => api.get('/equipos', { params }),
  get: (id) => api.get(`/equipos/${id}`),
  create: (payload) => api.post('/equipos', payload),
  update: (id, payload) => api.put(`/equipos/${id}`, payload),
}
