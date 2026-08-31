import api from './api'

export const envioService = {
  list: (params) => api.get('/envios', { params }),
  paged: (params) => api.get('/envios/paginado', { params }),
  get: (id) => api.get(`/envios/${id}`),
  create: (payload) => api.post('/envios', payload),
  update: (id, payload) => api.put(`/envios/${id}`, payload),
  history: (id) => api.get(`/envios/${id}/historial`),
  changeState: (id, payload) => api.post(`/envios/${id}/estado`, payload),
  deliverToTransport: (id, payload = {}) => api.post(`/envios/${id}/entrega-transportacion`, payload),
  deliverPrivate: (id, payload = {}) => api.post(`/envios/${id}/entrega-privada`, payload),
  registerTechnologyArrival: (id, payload = {}) => api.post(`/envios/${id}/llegada-tecnologia`, payload),
  confirmTransportArrival: (id, payload = {}) => api.post(`/envios/${id}/confirmar-llegada-transportacion`, payload),
  equipment: (id) => api.get(`/envio-equipos/por-envio/${id}`),
  ticketAvailable: (ticket, params = {}) => api.get(`/envio-equipos/ticket-disponible/${encodeURIComponent(ticket)}`, { params }),
  addEquipment: (payload) => api.post('/envio-equipos', payload),
  updateEquipment: (id, payload) => api.put(`/envio-equipos/${id}`, payload),
  removeEquipment: (id) => api.delete(`/envio-equipos/${id}`),
  reception: (id) => api.get(`/recepciones/por-envio/${id}`),
  incidents: (id) => api.get(`/incidencias/por-envio/${id}`),
}
