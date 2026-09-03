import api from './api'

export const casoService = {
  // Casos abiertos cuyo equipo está hoy en Tecnología: los candidatos a descarte.
  list: (params) => api.get('/casos', { params }),
  // Caso abierto de un equipo: de aquí sale el ticket que hereda el envío.
  byEquipment: (equipoId) => api.get(`/casos/equipo/${equipoId}`),
  discard: (payload) => api.post('/casos/descartar', payload),
}
