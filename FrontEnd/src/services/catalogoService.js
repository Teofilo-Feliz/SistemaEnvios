import api from './api'

export const catalogoService = {
  locations: (params) => api.get('/ubicaciones', { params }),
  location: (id) => api.get(`/ubicaciones/${id}`),
  types: (params) => api.get('/tipos-equipos', { params }),
  type: (id) => api.get(`/tipos-equipos/${id}`),
  states: (params) => api.get('/estados-envio', { params }),
  transitions: (params) => api.get('/estados-envio/transiciones', { params }),
  transportTypes: (params) => api.get('/tipos-transporte', { params }),
  createTransportType: (data) => api.post('/tipos-transporte', data),
  updateTransportType: (id, data) => api.put(`/tipos-transporte/${id}`, data),
  toggleTransportType: (id, activo) => api.patch(`/tipos-transporte/${id}/activo`, { activo }),
  internalDrivers: (params) => api.get('/choferes-internos', { params }),
  createInternalDriver: (data) => api.post('/choferes-internos', data),
  updateInternalDriver: (id, data) => api.put(`/choferes-internos/${id}`, data),
  toggleInternalDriver: (id, activo) => api.patch(`/choferes-internos/${id}/activo`, { activo }),
}
