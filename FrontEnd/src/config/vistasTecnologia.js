/**
 * Las pantallas de envíos del módulo de Tecnología, en un solo lugar.
 *
 * Antes eran tres tarjetas apiladas en una misma vista y cuatro enlaces de menú que solo se
 * diferenciaban en un `?view=` que el componente nunca leía: elegías una y salían todas.
 *
 * Cada pantalla declara aquí los estados que consulta. Eso importa más de lo que parece: la
 * versión anterior pedía una sola página de diez envíos mezclando los tres estados y filtraba
 * en el cliente, así que una sección podía salir vacía teniendo registros, solo que en otra
 * página. Ahora cada pantalla pagina lo suyo.
 */
export const VISTAS_TECNOLOGIA = {
  pendientes: {
    ruta: "/tecnologia/pendientes",
    titulo: "Equipos pendientes",
    subtitulo: "Envíos disponibles para retirar de Transportación",
    estados: ["ESPERA_TECNOLOGIA"],
    // Retirar el envío de Transportación se hace desde aquí, sin salir de la lista.
    accion: "recibir",
    textoAccion: "Recibir equipos",
    columnas: [
      { key: "number", label: "Envío" },
      { key: "transport", label: "Transporte" },
      { key: "status", label: "Estado" },
    ],
  },

  revision: {
    ruta: "/tecnologia/revision",
    titulo: "En revisión",
    subtitulo: "Listos para verificar equipo por equipo",
    estados: ["RECIBIDO_TRANSPORTACION", "EN_REVISION"],
    // Verificar exige la pantalla de recepción: se cierra equipo por equipo.
    accion: "verificar",
    textoAccion: "Verificar equipos y completar recepción",
    columnas: [
      { key: "number", label: "Envío" },
      { key: "description", label: "Descripción" },
      { key: "transport", label: "Transporte" },
      { key: "status", label: "Estado" },
    ],
  },

  revisados: {
    ruta: "/tecnologia/revisados",
    titulo: "Revisados",
    subtitulo: "Envíos que Tecnología ya recibió y verificó",
    // Se incluyen los recibidos con incidencia: el envío termina igual, lo que sigue abierto es
    // el caso del equipo. Dejarlos fuera escondería justo los que hay que seguir.
    estados: ["RECIBIDO_TECNOLOGIA", "RECIBIDO_TECNOLOGIA_INCIDENCIA"],
    accion: null,
    columnas: [
      { key: "number", label: "Envío" },
      { key: "description", label: "Descripción" },
      { key: "transport", label: "Transporte" },
      { key: "status", label: "Estado" },
    ],
  },
};

/** Configuración de la vista, o null si el nombre no corresponde a ninguna. */
export const vistaTecnologia = (nombre) => VISTAS_TECNOLOGIA[nombre] ?? null;
