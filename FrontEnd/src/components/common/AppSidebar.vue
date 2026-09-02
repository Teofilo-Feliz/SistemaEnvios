<script setup>
import { ref } from "vue";
import { useRoute } from "vue-router";
import logo from "@/assets/img/logitrack.webp";
import {
  BarChart3,
  Boxes,
  Building2,
  ChevronDown,
  CircleGauge,
  ClipboardCheck,
  PackageSearch,
  Settings,
  Truck,
  X,
} from "lucide-vue-next";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
const route = useRoute();
const ui = useUiStore();
const auth = useAuthStore();
const opened = ref(
  JSON.parse(localStorage.getItem("sidebar_groups") || '["envios"]'),
);
const groups = [
  {
    key: "envios",
    label: "Envíos",
    icon: PackageSearch,
    items: [
      { label: "Todos los envíos", to: "/envios" },
      {
        label: "Crear envío",
        to: "/envios/nuevo",
        permission: "canCreateShipment",
      },
    ],
  },
  {
    key: "equipos",
    label: "Equipos",
    icon: Boxes,
    items: [
      { label: "Todos los equipos", to: "/equipos" },
      {
        label: "Registrar equipo",
        to: "/equipos/nuevo",
        permission: "canCreateShipment",
      },
    ],
  },
  {
    key: "transportacion",
    label: "Transportación",
    icon: Truck,
    items: [
      { label: "Dashboard", to: "/transportacion" },
      {
        label: "Confirmaciones",
        to: "/transportacion/confirmaciones",
        permission: "transportes.confirmar",
      },
      { label: "Operaciones", to: "/transportacion/operaciones" },
      {
        label: "Asignar transporte a envío",
        to: "/transportacion/nuevo",
        permission: "transportes.gestionar",
      },
    ],
  },
  {
    key: "tecnologia",
    label: "Tecnología",
    icon: ClipboardCheck,
    items: [
      { label: "Notificaciones", to: "/tecnologia/notificaciones" },
      { label: "Nuevo envío a filial", to: "/tecnologia/envios/nuevo", permission: "canCreateShipment" },
      { label: "Equipos pendientes", to: "/tecnologia?view=pending" },
      { label: "En revisión", to: "/tecnologia?view=review" },
      { label: "Revisados", to: "/tecnologia?view=completed" },
      { label: "Incidencias", to: "/incidencias" },
    ],
  },
  {
    key: "seguimiento",
    label: "Seguimiento",
    icon: PackageSearch,
    items: [
      { label: "Buscar envío", to: "/seguimiento/buscar" },
      { label: "Historial", to: "/seguimiento/historial" },
      { label: "Trazabilidad", to: "/seguimiento/trazabilidad" },
    ],
  },
  {
    key: "filial",
    label: "Filial",
    icon: Building2,
    items: [{ label: "Recepciones pendientes", to: "/filial/recepciones", permission: "canReceiveShipment" }],
  },
  {
    key: "catalogos",
    label: "Catálogos",
    icon: Building2,
    permission: "canManageCatalogs",
    items: [
      { label: "Transportes y choferes", to: "/catalogos/transportes" },
      ...[
        "Filiales",
        "Ubicaciones",
        "Tipos de equipos",
        "Marcas",
        "Modelos",
        "Estados",
      ].map((label) => ({
        label,
        to: `/catalogos/${label.toLowerCase().replaceAll(" ", "-")}`,
      })),
    ],
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
function toggle(key) {
  opened.value = opened.value.includes(key)
    ? opened.value.filter((item) => item !== key)
    : [...opened.value, key];
  localStorage.setItem("sidebar_groups", JSON.stringify(opened.value));
}
function active(to) {
  return (
    route.fullPath === to || (to === "/envios" && route.path === "/envios")
  );
}
</script>
<template>
  <aside class="sidebar" :class="{ open: ui.mobileOpen }">
    <div class="sidebar-brand">
      <div class="brand-mark"><img :src="logo" alt="LogiTrack" /></div>
      <div class="brand-copy">
        <strong>LogiTrack</strong><span>Gestión tecnológica</span>
      </div>
      <button class="icon-btn mobile-only" @click="ui.mobileOpen = false">
        <X :size="20" />
      </button>
    </div>
    <nav class="sidebar-nav" aria-label="Navegación principal">
      <RouterLink
        class="nav-main"
        to="/dashboard"
        title="Dashboard"
        @click="ui.mobileOpen = false"
        ><CircleGauge :size="19" /><span>Dashboard</span></RouterLink
      ><template v-for="group in groups" :key="group.key"
        ><div v-if="auth.can(group.permission)" class="nav-group">
          <button
            class="nav-group-button"
            :title="group.label"
            @click="toggle(group.key)"
          >
            <component :is="group.icon" :size="19" /><span>{{
              group.label
            }}</span
            ><ChevronDown
              class="nav-chevron"
              :class="{ rotated: opened.includes(group.key) }"
              :size="15"
            />
          </button>
          <div
            v-show="opened.includes(group.key) && !ui.collapsed"
            class="nav-submenu"
          >
            <RouterLink
              v-for="item in group.items.filter((i) => auth.can(i.permission))"
              :key="item.to"
              :to="item.to"
              :class="{ active: active(item.to) }"
              @click="ui.mobileOpen = false"
              >{{ item.label }}</RouterLink
            >
          </div>
        </div></template
      >
    </nav>
    <div class="sidebar-footer">
      <BarChart3 :size="17" /><span>LogiTrack · 2026</span>
    </div>
  </aside>
</template>
