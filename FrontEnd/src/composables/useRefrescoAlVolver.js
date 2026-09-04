import { onMounted, onUnmounted } from "vue";

/**
 * Vuelve a pedir los datos cuando la pestaña recupera el foco.
 *
 * Las pantallas cargaban al montarse y no volvían a mirar, así que quien tenía dos pestañas
 * abiertas veía datos viejos hasta pulsar "Actualizar". Esto cubre el patrón humano real:
 * miras una pantalla, actúas en otro sitio, y al volver esperas verlo reflejado.
 *
 * No cubre estar mirando la pantalla mientras otra persona actúa: para eso hace falta sondeo
 * o tiempo real. Es deliberadamente lo barato que resuelve la mayor parte.
 *
 * @param {() => unknown} recargar  Lo que hay que volver a ejecutar.
 * @param {{ minimoEntreRecargas?: number }} opciones
 */
export function useRefrescoAlVolver(recargar, { minimoEntreRecargas = 5000 } = {}) {
  let ultima = Date.now();

  function alVolver() {
    if (document.visibilityState !== "visible") return;
    // Alt-tab repetido no debe convertirse en una ráfaga de peticiones.
    if (Date.now() - ultima < minimoEntreRecargas) return;
    ultima = Date.now();
    recargar();
  }

  onMounted(() => {
    document.addEventListener("visibilitychange", alVolver);
    window.addEventListener("focus", alVolver);
  });

  onUnmounted(() => {
    document.removeEventListener("visibilitychange", alVolver);
    window.removeEventListener("focus", alVolver);
  });
}
