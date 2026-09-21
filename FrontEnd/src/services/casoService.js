import api from "./api";

export const casoService = {
  // Casos abiertos cuyo equipo está hoy en Tecnología: los candidatos a descarte.
  list: (params) => api.get("/casos", { params }),
  // Caso abierto de un equipo: de aquí sale el ticket que hereda el envío.
  byEquipment: (equipoId) => api.get(`/casos/equipo/${equipoId}`),
  // El inventario de Tecnología para armar un envío: cada equipo con el ticket de su caso.
  // Filtrado por filial devuelve los de esa filial más los que no son de ninguna.
  equiposEnTecnologia: (params) => api.get("/casos/equipos-en-tecnologia", { params }),
  discard: (payload) => api.post("/casos/descartar", payload),
};
