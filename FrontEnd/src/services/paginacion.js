// El API pagina todo, incluidos los catálogos. Una tabla dibuja una página a la vez, pero un
// <select> necesita las opciones completas o pierde valores en silencio: por eso hay dos formas
// de consumir un listado, y ninguna trae la tabla entera de una sola vez.

/** Página vacía, para inicializar sin casos especiales en las vistas. */
export const paginaVacia = { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 };

/** Normaliza la respuesta del API a una página, tolerando un cuerpo inesperado. */
export function aPagina(response) {
  const data = response?.data;
  if (!data || !Array.isArray(data.items)) return { ...paginaVacia };
  return data;
}

/** Solo las filas, para cuando la vista maneja el total por separado. */
export function filas(response) {
  return aPagina(response).items;
}

/**
 * Recorre las páginas hasta juntar el catálogo completo. Es para combos, no para tablas: la
 * base recibe consultas acotadas en todos los casos, y el tope evita que un catálogo que creció
 * sin control se traiga entero por descuido.
 */
export async function todasLasPaginas(consulta, { pageSize = 100, maxPaginas = 20 } = {}) {
  const acumulado = [];
  for (let page = 1; page <= maxPaginas; page += 1) {
    const pagina = aPagina(await consulta({ page, pageSize }));
    acumulado.push(...pagina.items);
    if (acumulado.length >= pagina.totalItems || pagina.items.length === 0) break;
  }
  return acumulado;
}
