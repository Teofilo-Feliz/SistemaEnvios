import api from "./api";

export const glpiService = {
  // Equipo asociado al ticket, para autocompletar. Devuelve { ticket, equipo } y equipo puede
  // venir null: hay tickets sin activo asociado y eso no es un error.
  equipoDeTicket: (ticket) => api.get(`/glpi/ticket/${encodeURIComponent(ticket)}/equipo`),
};
