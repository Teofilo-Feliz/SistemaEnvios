import api from './api'

export const transporteService = {
  getByEnvio: (envioId) => api.get(`/transportes/por-envio/${envioId}`),
  create: (payload) => api.post('/transportes', payload),
  update: (id, payload) => api.put(`/transportes/${id}`, payload),
  confirm: (id) => api.post(`/transportes/${id}/confirmacion`),
}
