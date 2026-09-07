import api from "./api";

export const notificacionService = {
  listTechnology: (params = {}) =>
    api.get("/notificaciones", { params: { rol: "TECNOLOGIA", ...params } }),
};
