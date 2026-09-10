export const isInternalTransport = (value) =>
  value === 1 || value === "TransportacionInstitucional";

export const isPrivateTransport = (value) => value === 2 || value === "EntregaDirectaTecnologia";

/**
 * Valores de EstrategiaTransporteEnum, para pedírselos al API.
 *
 * El institucional pasa por Transportación; el privado va de la filial directo a Tecnología y no
 * toca Transportación en ningún momento. Esa distinción decide qué envíos ve cada módulo, así que
 * las pantallas la piden al servidor en vez de descartar en el navegador: filtrar aquí rompería
 * los totales y la paginación.
 */
export const ESTRATEGIA_INSTITUCIONAL = 1;
export const ESTRATEGIA_PRIVADA = 2;
