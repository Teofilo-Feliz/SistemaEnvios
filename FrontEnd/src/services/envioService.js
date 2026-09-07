import api from "./api";

export const envioService = {
  // Un solo camino: el listado de envíos siempre viene paginado y filtrado en el servidor.
  paged: (params) => api.get("/envios", { params }),
  get: (id) => api.get(`/envios/${id}`),
  create: (payload) => api.post("/envios", payload),
  createWithEquipment: (payload) => api.post("/envios/con-equipos", payload),
  update: (id, payload) => api.put(`/envios/${id}`, payload),
  history: (id, params) => api.get(`/envios/${id}/historial`, { params }),
  changeState: (id, payload) => api.post(`/envios/${id}/estado`, payload),
  deliverToTransport: (id, payload = {}) =>
    api.post(`/envios/${id}/entrega-transportacion`, payload),
  deliverPrivate: (id, payload = {}) => api.post(`/envios/${id}/entrega-privada`, payload),
  registerTechnologyArrival: (id, payload = {}) =>
    api.post(`/envios/${id}/llegada-tecnologia`, payload),
  confirmTransportArrival: (id, payload = {}) =>
    api.post(`/envios/${id}/confirmar-llegada-transportacion`, payload),
  dispatchFromTechnology: (id, payload = {}) =>
    api.post(`/envios/${id}/despachar-tecnologia`, payload),
  equipment: (id, params) => api.get(`/envio-equipos/por-envio/${id}`, { params }),
  ticketAvailable: (ticket, params = {}) =>
    api.get(`/envio-equipos/ticket-disponible/${encodeURIComponent(ticket)}`, { params }),
  addEquipment: (payload) => api.post("/envio-equipos", payload),
  updateEquipment: (id, payload) => api.put(`/envio-equipos/${id}`, payload),
  removeEquipment: (id) => api.delete(`/envio-equipos/${id}`),
  reception: (id) => api.get(`/recepciones/por-envio/${id}`),
  incidents: (id, params) => api.get(`/incidencias/por-envio/${id}`, { params }),
  // Los equipos que llegaron mal en la recepción, con lo que anotó quien recibió. Es distinto
  // de las incidencias registradas a mano: esto sale de verificar equipo por equipo.
  receptionIncidents: (id, params) =>
    api.get(`/recepciones/incidencias/por-envio/${id}`, { params }),
};
