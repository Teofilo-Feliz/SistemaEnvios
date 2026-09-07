import api from './api'

export const glpiService = {
  // Existencia de un ticket en la mesa de ayuda. Devuelve { itemType, id, existe }.
  ticketExiste: (ticket) => api.get(`/glpi/ticket/${encodeURIComponent(ticket)}/existe`),
}
