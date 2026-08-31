import api from './api'

export const incidenciaService = {
  byShipment: (envioId) => api.get(`/incidencias/por-envio/${envioId}`),
  create: (payload) => api.post('/incidencias', payload),
}
