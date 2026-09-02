import api from './api'
export const recepcionService = {
  create: (payload) => api.post('/recepciones', payload),
  verify: (id, payload) => api.post(`/recepciones/${id}/equipos/verificacion`, { recepcionId: id, ...payload }),
  complete: (id) => api.post(`/recepciones/${id}/finalizacion`),
}
