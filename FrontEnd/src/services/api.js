import axios from "axios";

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "/api",
  timeout: 20000,
  headers: { "Content-Type": "application/json" },
  // Los arrays viajan como claves repetidas —estadoCodigos=A&estadoCodigos=B— que es la única
  // forma que ASP.NET Core enlaza a un string[] de la petición.
  //
  // Por defecto axios los manda como estadoCodigos[]=A, y eso no falla: el binder devuelve un
  // array VACÍO en vez de null, así que la condición `EstadoCodigos is { Length: > 0 }` del
  // servicio da falso y la consulta se ejecuta SIN filtro. El backend responde 200 con todos los
  // envíos y la pantalla los muestra como si fueran los suyos: /tecnologia/pendientes listaba
  // envíos recién creados y ya cerrados, y los totales de la paginación eran los de la tabla
  // entera. Ningún error en ninguna capa.
  paramsSerializer: { indexes: null },
});

// El token se pide al store, que lo tiene en memoria y lo renueva si está vencido. No hay
// copia en localStorage: era una segunda credencial en disco, y además se enviaba vencida.
let obtenerToken = async () => null;
export function registrarProveedorDeToken(proveedor) {
  obtenerToken = proveedor;
}

api.interceptors.request.use(async (config) => {
  const token = await obtenerToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const status = error.response?.status;
    // Respaldo, no primera opción: solo se usa cuando el backend no dijo nada aprovechable.
    const respaldoPorEstado = {
      401: "Tu sesión ha expirado. Inicia sesión nuevamente.",
      403: "No tienes permisos para realizar esta acción.",
      500: "Ocurrió un error interno en el servidor.",
    };
    // El mensaje del backend manda. ToProblem() escribe en "detail" un texto pensado para quien
    // lo lee ("Este tablero es de las filiales; su usuario no pertenece a una"), y hasta el 500
    // de ApiExceptionHandler trae la referencia del trace para soporte.
    //
    // Este mapa iba primero y los tapaba todos: cualquier 403 del sistema decía la misma frase
    // genérica, daba igual lo que hubiera pasado. Eso convierte un problema de configuración en
    // una cacería, porque el único sitio donde estaba escrita la causa era la respuesta que el
    // interceptor acababa de descartar.
    //
    // El 401 es la excepción y por eso no lee el cuerpo: no es un mensaje de negocio sino estado
    // de sesión, y quien lo escribe es el validador de tokens ("The security token is missing"),
    // que al usuario no le dice nada.
    const delBackend =
      status === 401 ? null : error.response?.data?.detail || error.response?.data?.message;
    error.userMessage =
      delBackend || respaldoPorEstado[status] || "No fue posible completar la operación.";
    // Un 401 puede ser solo un token recién vencido. Se renueva y se reintenta una vez;
    // antes la petición se perdía y el usuario veía un error en una acción que iba a funcionar.
    if (status === 401 && error.config && !error.config._reintentado) {
      error.config._reintentado = true;
      const renovado = await new Promise((resolve) => {
        window.dispatchEvent(new CustomEvent("auth:unauthorized", { detail: { resolve } }));
        // Si nadie atiende el evento no se queda colgado esperando.
        setTimeout(() => resolve(null), 8000);
      });
      if (renovado) {
        error.config.headers.Authorization = `Bearer ${renovado}`;
        return api.request(error.config);
      }
    }
    return Promise.reject(error);
  },
);

export default api;
