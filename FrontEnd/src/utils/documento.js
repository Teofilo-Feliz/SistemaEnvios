/**
 * Documento del responsable de un transporte privado.
 *
 * Espeja FormatosDocumento.cs del backend, que es quien manda: aquí se filtra y se valida para que
 * el usuario no llegue a enviar algo que la API va a rechazar, pero la última palabra la tienen el
 * validador de la petición y el CHECK de la tabla.
 *
 * La cédula dominicana son once dígitos y nada más, así que una letra ahí es siempre un error de
 * tecleo y se impide escribirla. El pasaporte casi nunca es solo numérico —el dominicano empieza
 * por letras— y exigirle dígitos dejaría fuera a cualquier responsable extranjero.
 */

export const TIPO_CEDULA = 1;
export const TIPO_PASAPORTE = 2;

export const TIPOS_DOCUMENTO = [
  { valor: TIPO_CEDULA, etiqueta: "Cédula" },
  { valor: TIPO_PASAPORTE, etiqueta: "Pasaporte" },
];

const esPasaporte = (tipo) => Number(tipo) === TIPO_PASAPORTE;

/** Lo que se deja escribir. Se aplica al teclear, no solo al enviar. */
export function filtrarDocumento(tipo, valor) {
  const texto = String(valor ?? "");
  return esPasaporte(tipo)
    ? texto
        .replace(/[^A-Za-z0-9]/g, "")
        .toUpperCase()
        .slice(0, 15)
    : texto.replace(/\D/g, "").slice(0, 11);
}

export const largoMaximo = (tipo) => (esPasaporte(tipo) ? 15 : 11);
export const etiquetaDocumento = (tipo) => (esPasaporte(tipo) ? "Pasaporte" : "Cédula");
export const modoEntrada = (tipo) => (esPasaporte(tipo) ? "text" : "numeric");

/** Mensaje de error, o cadena vacía si el documento está bien. */
export function errorDocumento(tipo, valor) {
  const limpio = filtrarDocumento(tipo, valor);
  if (esPasaporte(tipo)) {
    return /^[A-Z0-9]{6,15}$/.test(limpio)
      ? ""
      : "El pasaporte debe tener entre 6 y 15 letras o números.";
  }
  return /^\d{11}$/.test(limpio) ? "" : "La cédula debe tener exactamente 11 dígitos.";
}

/** Solo dígitos, para el código de activo. */
export const filtrarSoloDigitos = (valor) => String(valor ?? "").replace(/\D/g, "");
