/**
 * Un objeto de errores solo guarda mensajes, así que solo un texto con contenido cuenta.
 *
 * Con `.some(Boolean)` bastaba con que alguien metiera ahí un valor de formulario para bloquear
 * el guardado para siempre y sin decir nada: el número 1 es truthy, no se pinta en ningún campo,
 * y el botón queda muerto. Pasó con documentType y costó encontrarlo.
 */
export function hayErrores(errores) {
  return Object.values(errores).some((valor) => typeof valor === "string" && valor.length > 0);
}
