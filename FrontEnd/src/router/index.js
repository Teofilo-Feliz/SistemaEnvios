import { createRouter, createWebHistory } from "vue-router";
import AppLayout from "@/layouts/AppLayout.vue";
import { useAuthStore } from "@/stores/authStore";
import { rutaPermitida } from "@/config/modulos";
import { userManager } from "@/services/authService";
const PlaceholderView = () => import("@/views/PlaceholderView.vue");
const routes = [
  {
    path: "/callback",
    name: "Callback",
    component: () => import("@/views/auth/OidcCallbackView.vue"),
  },
  {
    path: "/silent-renew",
    name: "SilentRenew",
    component: () => import("@/views/auth/OidcSilentRenewView.vue"),
  },
  {
    path: "/signed-out",
    name: "SignedOut",
    component: () => import("@/views/auth/SignedOutView.vue"),
  },
  { path: "/logout", name: "Logout", component: () => import("@/views/auth/LogoutView.vue") },
  {
    path: "/unauthorized",
    name: "Unauthorized",
    component: () => import("@/views/auth/UnauthorizedView.vue"),
  },
  {
    path: "/login",
    name: "login",
    component: () => import("@/views/auth/LoginView.vue"),
  },
  // La raíz no decide aquí a dónde va el usuario. Un `redirect` se evalúa al resolver la ruta,
  // antes de que el guard cargue el perfil, así que siempre veía perfilNombre en null y elegía
  // el destino equivocado. Ahora solo aterriza, y el guard —que sí tiene el perfil— redirige.
  // El guard siempre redirige desde aquí, así que este componente no llega a verse; es solo
  // para que la ruta resuelva. No puede ser una pantalla con lógica propia.
  {
    path: "/",
    name: "inicio",
    component: { template: '<div class="page-loading">Entrando…</div>' },
  },
  {
    path: "/",
    component: AppLayout,
    children: [
      {
        path: "dashboard",
        name: "dashboard",
        component: () => import("@/views/dashboard/DashboardView.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "envios",
        name: "envios",
        component: () => import("@/views/envios/EnviosList.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "envios/nuevo",
        name: "envio-create",
        component: () => import("@/views/envios/EnvioCreate.vue"),
        meta: { permission: "envios.crear" },
      },
      {
        path: "envios/:id",
        name: "envio-detail",
        component: () => import("@/views/envios/EnvioDetail.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "tecnologia/envios/nuevo",
        name: "technology-shipment-create",
        component: () => import("@/views/envios/EnvioCreate.vue"),
        meta: { permission: "envios.crear" },
      },
      {
        path: "filial/recepciones",
        name: "filial-receptions",
        component: () => import("@/views/filial/FilialRecepciones.vue"),
        meta: { permission: "recepciones.gestionar" },
      },
      {
        path: "envios/:id/editar",
        name: "envio-edit",
        component: () => import("@/views/envios/EnvioCreate.vue"),
        meta: { permission: "envios.editar" },
      },
      {
        path: "equipos",
        name: "equipos",
        component: () => import("@/views/equipos/EquiposList.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "equipos/nuevo",
        name: "equipo-create",
        component: () => import("@/views/equipos/EquipoCreate.vue"),
        meta: { permission: "equipos.gestionar" },
      },
      {
        path: "equipos/:id",
        name: "equipo-detail",
        component: () => import("@/views/equipos/EquipoDetail.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "transportacion",
        name: "transportacion",
        component: () => import("@/views/transportacion/TransportacionDashboard.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "transportacion/operaciones",
        name: "transportacion-operations",
        component: () => import("@/views/transportacion/TransportacionView.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "transportacion/nuevo",
        name: "transportacion-create",
        component: () => import("@/views/transportacion/TransportacionCreate.vue"),
        meta: { permission: "transportes.gestionar" },
      },
      {
        // Los tipos de transporte y los choferes son de Transportación, no un catálogo
        // general de Tecnología. Exige 'transportes.administrar', que es mantener la flota;
        // 'transportes.gestionar' es otra cosa y lo tienen las filiales para sus envíos.
        path: "transportacion/catalogos",
        name: "transportacion-catalogos",
        component: () => import("@/views/transportacion/TransportesCatalogos.vue"),
        meta: { permission: "transportes.administrar" },
      },
      {
        // Descartar es lo que cierra un caso que no va a terminar con el equipo de vuelta en
        // su filial; exige gestionar equipos, no solo consultarlos.
        path: "tecnologia/descartes",
        name: "tecnologia-descartes",
        component: () => import("@/views/tecnologia/TecnologiaDescartes.vue"),
        meta: { permission: "equipos.gestionar" },
      },
      {
        path: "tecnologia/notificaciones",
        name: "tecnologia-notifications",
        component: () => import("@/views/tecnologia/TecnologiaNotificaciones.vue"),
        meta: { permission: "envios.consultar" },
      },
      // Una sola pantalla de recepción para las dos direcciones del flujo: el backend ya
      // distingue por dirección qué estados admiten recibir y verificar.
      {
        path: "tecnologia/recepciones/:envioId",
        name: "tecnologia-reception",
        component: () => import("@/views/recepciones/RecepcionEnvio.vue"),
        meta: { permission: "recepciones.gestionar" },
      },
      {
        // Tablero propio de la filial. Consultar envíos basta: el resto del módulo son las
        // pantallas que ya existen, acotadas por el alcance del backend.
        path: "filial",
        name: "filial",
        component: () => import("@/views/filial/FilialDashboard.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "filial/recepciones/:envioId",
        name: "filial-reception",
        component: () => import("@/views/recepciones/RecepcionEnvio.vue"),
        meta: { permission: "recepciones.gestionar" },
      },
      // Las cuatro pantallas del modulo apilaban su contenido en una sola vista y se
      // distinguian por un "?view=" que el componente nunca leia: elegias una y salian todas.
      // Con ruta propia cada una carga lo suyo y el menu marca la activa sin ambiguedad.
      { path: "tecnologia", redirect: "/tecnologia/pendientes" },
      {
        path: "tecnologia/pendientes",
        name: "tecnologia-pendientes",
        component: () => import("@/views/tecnologia/TecnologiaEnvios.vue"),
        meta: { permission: "envios.consultar", vista: "pendientes" },
      },
      {
        path: "tecnologia/revision",
        name: "tecnologia-revision",
        component: () => import("@/views/tecnologia/TecnologiaEnvios.vue"),
        meta: { permission: "envios.consultar", vista: "revision" },
      },
      {
        path: "tecnologia/revisados",
        name: "tecnologia-revisados",
        component: () => import("@/views/tecnologia/TecnologiaEnvios.vue"),
        meta: { permission: "envios.consultar", vista: "revisados" },
      },
      {
        path: "tecnologia/equipos",
        name: "tecnologia-equipos",
        component: () => import("@/views/tecnologia/TecnologiaEquipos.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "incidencias",
        name: "incidencias",
        component: () => import("@/views/incidencias/IncidenciasView.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        // La ruta vieja vivía en Catálogos. Se conserva como redirección para que no se
        // rompan los enlaces guardados ni las pestañas que alguien tenga abiertas.
        path: "catalogos/transportes",
        redirect: { name: "transportacion-catalogos" },
      },
      {
        path: "catalogos/:section?",
        name: "catalogos",
        component: PlaceholderView,
        meta: { permission: "catalogos.administrar", title: "Catálogos" },
      },
      {
        path: "administracion/:section?",
        name: "administracion",
        component: PlaceholderView,
        meta: { permission: "catalogos.administrar", title: "Administración" },
      },
      {
        path: "perfil",
        name: "profile",
        component: PlaceholderView,
        meta: { permission: "envios.consultar", title: "Mi perfil" },
      },
      {
        path: "preferencias",
        name: "preferences",
        component: PlaceholderView,
        meta: { permission: "envios.consultar", title: "Preferencias" },
      },
    ],
  },
  // Igual que la raíz: una URL inexistente aterriza y el guard decide con el perfil ya cargado.
  {
    path: "/:pathMatch(.*)*",
    name: "no-encontrado",
    component: { template: '<div class="page-loading">Entrando…</div>' },
  },
];
// Dos bandejas en vez de una: mezclar "el chofer recibió el equipo" con "el envío llegó" se
// prestaba a confusión, porque son momentos distintos del mismo traslado. Comparten componente
// porque el trabajo es idéntico; lo que cambia es qué se confirma.
routes
  .find((route) => route.component === AppLayout)
  ?.children.push(
    {
      path: "transportacion/recepcion-chofer",
      name: "transportacion-custodia",
      component: () => import("@/views/transportacion/TransportacionBandeja.vue"),
      props: { modo: "custodia" },
      meta: { permission: "transportes.confirmar" },
    },
    {
      path: "transportacion/llegadas",
      name: "transportacion-llegadas",
      component: () => import("@/views/transportacion/TransportacionBandeja.vue"),
      props: { modo: "llegada" },
      meta: { permission: "transportes.confirmar" },
    },
  );
const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior: () => ({ top: 0 }),
});
const publicRoutes = ["Callback", "SilentRenew", "Logout", "SignedOut"];
router.beforeEach(async (to) => {
  const auth = useAuthStore();
  if (publicRoutes.includes(to.name)) return true;
  if (!auth.isAuthenticated) await auth.checkSession();
  // Los permisos efectivos los resuelve el backend a partir de la posicion: sin el perfil
  // cargado, auth.can() responderia con los permisos de otra aplicacion que trae el token.
  if (auth.isAuthenticated && !auth.perfil) await auth.cargarPerfil();
  if (to.name === "login") return true;
  if (!auth.isAuthenticated) {
    if (userManager) {
      await auth.forceLogoutAndRedirectToLogin(to.fullPath);
      return false;
    }
    return { name: "login" };
  }
  if (to.name === "Unauthorized") return true;

  // "Llévame a donde me toca". Se resuelve aquí y no en un redirect de la ruta porque este es
  // el primer punto donde el perfil ya está cargado. Un perfil que el backend no supo
  // clasificar aterriza en /unauthorized, no en el tablero general.
  if (to.name === "inicio" || to.name === "no-encontrado") return { path: auth.moduloInicio };

  // Cada perfil trabaja dentro de su módulo. Ocultarlo del menú no basta: la URL escrita a
  // mano o un enlace viejo llegarían igual.
  if (!rutaPermitida(auth.perfilNombre, to.path)) {
    const inicio = auth.moduloInicio;
    return { path: inicio === to.path ? "/unauthorized" : inicio };
  }

  const permission = to.meta.permission;
  if (!permission || auth.can(permission)) return true;
  const inicio = auth.moduloInicio;
  return { path: inicio === to.path ? "/unauthorized" : inicio };
});
export default router;
