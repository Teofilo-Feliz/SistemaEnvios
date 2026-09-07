import api from "./api";

export const transporteService = {
  // Dos bandejas separadas: el chofer recibió el equipo, y el envío llegó. El servidor las
  // filtra y pagina, así que la pantalla no consulta el transporte de cada envío por su cuenta.
  pendingCustody: (params) => api.get("/transportes/pendientes-custodia", { params }),
  pendingArrival: (params) => api.get("/transportes/pendientes-llegada", { params }),
  getByEnvio: (envioId) => api.get(`/transportes/por-envio/${envioId}`),
  create: (payload) => api.post("/transportes", payload),
  update: (id, payload) => api.put(`/transportes/${id}`, payload),
  confirm: (id) => api.post(`/transportes/${id}/confirmacion`),
};
