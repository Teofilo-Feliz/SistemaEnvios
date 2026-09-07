/**
 * Qué módulos ve cada perfil y dónde aterriza al entrar.
 *
 * La regla vive aquí y la usan las tres cosas que dependen de ella: el menú, el guardián de
 * rutas y la redirección de entrada. Si estuviera repartida, el menú podría ocultar un módulo
 * que la URL sigue abriendo, que es justo la clase de hueco que no se ve probando a mano.
 *
 * Los permisos son otra cosa y siguen aplicándose encima: el perfil dice a qué módulo entras,
 * el permiso dice qué puedes hacer dentro.
 */

/** Un perfil sin restricción de módulos. Tecnología es dueña del sistema. */
export const SIN_RESTRICCION = null;

/**
 * Un usuario que el backend no supo clasificar: sin posición mapeada, sin rol de este sistema.
 *
 * Existe porque antes no se distinguía de SIN_RESTRICCION. El backend devuelve el perfil
 * "SinAlcance" —literalmente "no tiene alcance"— y como ese nombre no está en el mapa, caía en
 * `?? SIN_RESTRICCION`, que significa lo contrario: acceso a todos los módulos. El mismo
 * agujero se abría cuando /api/perfil fallaba y el perfil quedaba en null.
 *
 * Ahora un perfil desconocido no concede nada: solo la pantalla que le explica por qué.
 */
export const SIN_ACCESO = {
  inicio: "/unauthorized",
  rutas: ["/unauthorized"],
  grupos: [],
};

export const MODULOS_POR_PERFIL = {
  Global: SIN_RESTRICCION,

  // Soporte técnico. En el backend ve los mismos datos que Global —Tecnología está en un
  // extremo de todo envío— pero trabaja dentro de su módulo: no administra catálogos, ni la
  // flota, ni la configuración del sistema. Eso es lo que acota esta entrada.
  Tecnologia: {
    inicio: "/tecnologia",
    // Necesita /envios y /equipos porque desde sus propias pantallas se abre el detalle de un
    // envío y la ficha de un equipo; sin ellas los enlaces de su módulo mueren en /unauthorized.
    rutas: ["/tecnologia", "/envios", "/equipos", "/incidencias", "/perfil", "/unauthorized"],
    grupos: ["tecnologia", "envios", "equipos"],
  },

  Filial: {
    inicio: "/filial",
    // Crear y consultar envíos vive en /envios, pero es parte del trabajo de la filial: son
    // las entradas "Crear envío" y "Mis envíos" de su propio menú.
    rutas: ["/filial", "/envios", "/perfil", "/unauthorized"],
    grupos: ["filial"],
  },

  Transportacion: {
    inicio: "/transportacion",
    // El detalle del envío se abre desde sus propias tablas, así que necesita /envios.
    rutas: ["/transportacion", "/envios", "/perfil", "/unauthorized"],
    grupos: ["transportacion"],
  },
};

/**
 * Configuración del perfil: null si no tiene restricción, SIN_ACCESO si no se le reconoce.
 *
 * Ojo con el `Object.hasOwn`: el perfil Global vale null a propósito, así que un `??` lo
 * confundiría con "no está en el mapa" y le quitaría el acceso a todo.
 */
export function modulosDe(perfil) {
  if (!perfil) return SIN_ACCESO;
  return Object.hasOwn(MODULOS_POR_PERFIL, perfil) ? MODULOS_POR_PERFIL[perfil] : SIN_ACCESO;
}

/** Dónde debe aterrizar este perfil al entrar al sistema. */
export function inicioDe(perfil) {
  return modulosDe(perfil)?.inicio || "/dashboard";
}

/** Si la ruta pertenece a algún módulo que este perfil puede abrir. */
export function rutaPermitida(perfil, ruta) {
  const modulos = modulosDe(perfil);
  if (!modulos) return true;
  return modulos.rutas.some((base) => ruta === base || ruta.startsWith(`${base}/`));
}

/** Si el grupo del menú corresponde a este perfil. */
export function grupoVisible(perfil, clave) {
  const modulos = modulosDe(perfil);
  if (!modulos) return true;
  return modulos.grupos.includes(clave);
}
