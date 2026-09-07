import api from "./api";

export const incidenciaService = {
  // Listado general paginado. Antes esta pantalla pedía todos los envíos y luego una consulta
  // de incidencias por cada uno: cientos de peticiones para llenar una sola tabla.
  list: (params) => api.get("/incidencias", { params }),
  byShipment: (envioId, params) => api.get(`/incidencias/por-envio/${envioId}`, { params }),
  create: (payload) => api.post("/incidencias", payload),
};
