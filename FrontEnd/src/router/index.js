import { createRouter, createWebHistory } from "vue-router";
import AppLayout from "@/layouts/AppLayout.vue";
const PlaceholderView = () => import("@/views/PlaceholderView.vue");
const routes = [
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
router.beforeEach((to) => {
  if (to.name === "login") return true;
  if (!localStorage.getItem("auth_token")) return { name: "login" };
  const permission = to.meta.permission;
  if (!permission) return true;
  const raw = localStorage.getItem("auth_permissions");
  if (!raw) return true;
  try {
    return JSON.parse(raw).includes(permission) ? true : { name: "dashboard" };
  } catch {
    return true;
  }
});
export default router;
