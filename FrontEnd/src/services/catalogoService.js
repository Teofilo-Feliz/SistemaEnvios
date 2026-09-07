import api from "./api";
import { todasLasPaginas } from "./paginacion";

// Los métodos en plural devuelven la respuesta cruda (paginada) para las pantallas que
// administran el catálogo; los `all*` recorren las páginas y devuelven el arreglo completo,
// que es lo que necesitan los <select> de los formularios.
const paged = (url) => (params) => api.get(url, { params });

export const catalogoService = {
  locations: paged("/ubicaciones"),
  location: (id) => api.get(`/ubicaciones/${id}`),
  types: paged("/tipos-equipos"),
  type: (id) => api.get(`/tipos-equipos/${id}`),
  states: paged("/estados-envio"),
  transitions: paged("/estados-envio/transiciones"),
  transportTypes: paged("/tipos-transporte"),
  internalDrivers: paged("/choferes-internos"),

  allLocations: (params = {}) =>
    todasLasPaginas((p) => api.get("/ubicaciones", { params: { ...params, ...p } })),
  allTypes: (params = {}) =>
    todasLasPaginas((p) => api.get("/tipos-equipos", { params: { ...params, ...p } })),
  allStates: (params = {}) =>
    todasLasPaginas((p) => api.get("/estados-envio", { params: { ...params, ...p } })),
  allTransportTypes: (params = {}) =>
    todasLasPaginas((p) => api.get("/tipos-transporte", { params: { ...params, ...p } })),
  allInternalDrivers: (params = {}) =>
    todasLasPaginas((p) => api.get("/choferes-internos", { params: { ...params, ...p } })),

  createTransportType: (data) => api.post("/tipos-transporte", data),
  updateTransportType: (id, data) => api.put(`/tipos-transporte/${id}`, data),
  toggleTransportType: (id, activo) => api.patch(`/tipos-transporte/${id}/activo`, { activo }),
  createInternalDriver: (data) => api.post("/choferes-internos", data),
  updateInternalDriver: (id, data) => api.put(`/choferes-internos/${id}`, data),
  toggleInternalDriver: (id, activo) => api.patch(`/choferes-internos/${id}/activo`, { activo }),
};
