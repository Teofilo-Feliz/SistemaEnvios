import { createRouter, createWebHistory } from "vue-router";
import AppLayout from "@/layouts/AppLayout.vue";
import { useAuthStore } from "@/stores/authStore";
import { userManager } from "@/services/authService";
const PlaceholderView = () => import("@/views/PlaceholderView.vue");
const routes = [
  { path: "/callback", name: "Callback", component: () => import("@/views/auth/OidcCallbackView.vue") },
  { path: "/silent-renew", name: "SilentRenew", component: () => import("@/views/auth/OidcSilentRenewView.vue") },
  { path: "/signed-out", name: "SignedOut", component: () => import("@/views/auth/SignedOutView.vue") },
  { path: "/logout", name: "Logout", component: () => import("@/views/auth/LogoutView.vue") },
  { path: "/unauthorized", name: "Unauthorized", component: () => import("@/views/auth/UnauthorizedView.vue") },
  {
    path: "/login",
    name: "login",
    component: () => import("@/views/auth/LoginView.vue"),
  },
  { path: "/", redirect: "/dashboard" },
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
        component: () =>
          import("@/views/transportacion/TransportacionDashboard.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "transportacion/operaciones",
        name: "transportacion-operations",
        component: () =>
          import("@/views/transportacion/TransportacionView.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "transportacion/nuevo",
        name: "transportacion-create",
        component: () =>
          import("@/views/transportacion/TransportacionCreate.vue"),
        meta: { permission: "transportes.gestionar" },
      },
      {
        path: "tecnologia/notificaciones",
        name: "tecnologia-notifications",
        component: () =>
          import("@/views/tecnologia/TecnologiaNotificaciones.vue"),
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
        path: "filial/recepciones/:envioId",
        name: "filial-reception",
        component: () => import("@/views/recepciones/RecepcionEnvio.vue"),
        meta: { permission: "recepciones.gestionar" },
      },
      {
        path: "tecnologia",
        name: "tecnologia",
        component: () => import("@/views/tecnologia/TecnologiaView.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "incidencias",
        name: "incidencias",
        component: () => import("@/views/incidencias/IncidenciasView.vue"),
        meta: { permission: "envios.consultar" },
      },
      {
        path: "seguimiento/:section?",
        name: "seguimiento",
        component: PlaceholderView,
        meta: { permission: "envios.consultar" },
      },
      {
        path: "catalogos/transportes",
        name: "transport-catalogs",
        component: () => import("@/views/catalogos/TransportesCatalogos.vue"),
        meta: { permission: "catalogos.administrar" },
      },
      {
        path: "catalogos/:section?",
        name: "catalogos",
        component: PlaceholderView,
        meta: { permission: "catalogos.administrar" },
      },
      {
        path: "administracion/:section?",
        name: "administracion",
        component: PlaceholderView,
        meta: { permission: "catalogos.administrar" },
      },
      {
        path: "perfil",
        name: "profile",
        component: PlaceholderView,
        meta: { permission: "envios.consultar" },
      },
      {
        path: "preferencias",
        name: "preferences",
        component: PlaceholderView,
        meta: { permission: "envios.consultar" },
      },
    ],
  },
  { path: "/:pathMatch(.*)*", redirect: "/dashboard" },
];
routes
  .find((route) => route.component === AppLayout)
  ?.children.push({
    path: "transportacion/confirmaciones",
    name: "transportacion-confirmations",
    component: () =>
      import("@/views/transportacion/TransportacionConfirmaciones.vue"),
    meta: { permission: "transportes.confirmar" },
  });
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
    if (userManager) { await auth.forceLogoutAndRedirectToLogin(to.fullPath); return false; }
    return { name: "login" };
  }
  if (to.name === "Unauthorized") return true;
  const permission = to.meta.permission;
  if (!permission || auth.can(permission)) return true;
  const first = auth.can("envios.consultar") ? "/dashboard" : auth.can("envios.crear") ? "/tecnologia/envios/nuevo" : auth.can("transportes.gestionar") ? "/transportacion" : auth.can("recepciones.gestionar") ? "/filial/recepciones" : "/unauthorized";
  return { path: first === to.path ? "/unauthorized" : first };
});
export default router;
