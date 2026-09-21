import { Boxes, Building2, ClipboardCheck, PackageSearch, Settings, Truck } from "lucide-vue-next";

/**
 * El menú lateral: los módulos y sus pantallas.
 *
 * Vive aquí y no dentro de AppSidebar porque la miga de pan necesita los mismos nombres. Cuando
 * estaba en el componente, la miga leía route.meta.title —que solo tenían 4 de 46 rutas— y decía
 * "Inicio" en casi todo el sistema. La alternativa era repetir cuarenta y dos etiquetas en el
 * router y mantenerlas a mano en dos sitios.
 *
 * El campo `permission` lo resuelve el menú con auth.can(); la miga no lo mira, porque solo
 * nombra la pantalla en la que el usuario ya está.
 */
export const GRUPOS = [
  {
    key: "envios",
    label: "Envíos",
    icon: PackageSearch,
    items: [
      { label: "Todos los envíos", to: "/envios" },
      { label: "Crear envío", to: "/envios/nuevo", permission: "canCreateShipment" },
    ],
  },
  {
    key: "equipos",
    label: "Equipos",
    icon: Boxes,
    items: [
      { label: "Todos los equipos", to: "/equipos" },
      { label: "Registrar equipo", to: "/equipos/nuevo", permission: "canCreateShipment" },
    ],
  },
  {
    key: "transportacion",
    label: "Transportación",
    icon: Truck,
    items: [
      { label: "Dashboard", to: "/transportacion" },
      {
        label: "Recepción del chofer",
        to: "/transportacion/recepcion-chofer",
        permission: "transportes.confirmar",
      },
      { label: "Llegadas", to: "/transportacion/llegadas", permission: "transportes.confirmar" },
      { label: "Operaciones", to: "/transportacion/operaciones" },
      {
        label: "Asignar transporte a envío",
        to: "/transportacion/nuevo",
        permission: "transportes.gestionar",
      },
      {
        label: "Transportes y choferes",
        to: "/transportacion/catalogos",
        permission: "transportes.administrar",
      },
    ],
  },
  {
    key: "tecnologia",
    label: "Tecnología",
    icon: ClipboardCheck,
    items: [
      { label: "Notificaciones", to: "/tecnologia/notificaciones" },
      {
        label: "Nuevo envío a filial",
        to: "/tecnologia/envios/nuevo",
        permission: "canCreateShipment",
      },
      // El orden es el del recorrido real de un equipo que llega. Las dos primeras son las dos
      // formas de recibir: el privado llega solo desde la filial, el institucional llega por
      // Transportación. Desde cualquiera de las dos se marca conforme o con incidencia.
      { label: "Por recibir", to: "/tecnologia/camino", permission: "recepciones.gestionar" },
      { label: "En revisión", to: "/tecnologia/revision" },
      { label: "Revisados", to: "/tecnologia/revisados" },
      { label: "Equipos en Tecnología", to: "/tecnologia/equipos" },
      {
        label: "Descarte de equipos",
        to: "/tecnologia/descartes",
        permission: "equipos.gestionar",
      },
      { label: "Incidencias", to: "/incidencias" },
    ],
  },
  {
    key: "filial",
    label: "Filial",
    icon: Building2,
    items: [
      { label: "Mi filial", to: "/filial" },
      { label: "Crear envío", to: "/envios/nuevo", permission: "canCreateShipment" },
      { label: "Mis envíos", to: "/envios" },
      { label: "Recibir envíos", to: "/filial/recepciones", permission: "canReceiveShipment" },
    ],
  },
  {
    key: "catalogos",
    label: "Catálogos",
    icon: Building2,
    permission: "canManageCatalogs",
    // Solo filiales. Las demás entradas apuntaban a la pantalla de "módulo en construcción":
    // ofrecían un catálogo que no existía y no había forma de saberlo hasta entrar.
    items: [{ label: "Filiales", to: "/catalogos/filiales" }],
  },
  {
    key: "admin",
    label: "Administración",
    icon: Settings,
    permission: "canViewAudit",
    items: [
      { label: "Configuración", to: "/administracion/configuracion" },
      { label: "Auditoría", to: "/administracion/auditoria" },
      { label: "Integraciones", to: "/administracion/integraciones" },
    ],
  },
];

/**
 * Dónde está el usuario, para la miga de pan: ["Módulo", "Pantalla"].
 *
 * Una pantalla puede estar en dos módulos —"Crear envío" aparece en Envíos y en Filial— y aquí
 * gana el primero. Da igual cuál: el nombre de la pantalla es el mismo y es lo que el usuario
 * reconoce.
 */
export function migaDeRuta(path, titulo = "") {
  for (const grupo of GRUPOS) {
    const exacta = grupo.items.find((item) => item.to === path);
    if (exacta) return [grupo.label, exacta.label];
  }

  // Las pantallas de detalle no están en el menú: /envios/123 cuelga de /envios. Se busca el
  // prefijo más largo para que /tecnologia/envios/nuevo no acabe bajo /tecnologia.
  let mejor = null;
  for (const grupo of GRUPOS) {
    for (const item of grupo.items) {
      if (!path.startsWith(`${item.to}/`)) continue;
      if (!mejor || item.to.length > mejor.item.to.length) mejor = { grupo, item };
    }
  }
  if (mejor) return [mejor.grupo.label, mejor.item.label, titulo].filter(Boolean);

  // Y si la ruta no cuelga de ninguna entrada -/tecnologia/recepciones/9 no está en el menú-,
  // al menos se nombra el módulo por el primer tramo de la URL.
  const modulo = path.split("/").filter(Boolean)[0];
  const raiz = `/${modulo}`;
  const grupo = modulo
    ? GRUPOS.find((g) => g.items.some((item) => item.to === raiz || item.to.startsWith(`${raiz}/`)))
    : null;
  if (grupo) return [grupo.label, titulo].filter(Boolean);

  return titulo ? [titulo] : [];
}
