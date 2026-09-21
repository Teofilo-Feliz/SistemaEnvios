<script setup>
import { ref } from "vue";
import { useRoute } from "vue-router";
import logo from "@/assets/img/logitrack.webp";
import { ChevronDown, CircleGauge, X } from "lucide-vue-next";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
import { grupoVisible } from "@/config/modulos";
import { GRUPOS as groups } from "@/config/menu";
const route = useRoute();
const ui = useUiStore();
const auth = useAuthStore();
const opened = ref(JSON.parse(localStorage.getItem("sidebar_groups") || '["envios"]'));
function toggle(key) {
  opened.value = opened.value.includes(key)
    ? opened.value.filter((item) => item !== key)
    : [...opened.value, key];
  localStorage.setItem("sidebar_groups", JSON.stringify(opened.value));
}
function active(to) {
  // Se compara la ruta y no fullPath: las pantallas ya no se distinguen por query, y
  // comparar la query completa dejaba de marcar el activo en cuanto la URL llevaba un filtro.
  return route.path === to;
}
</script>
<template>
  <aside class="sidebar" :class="{ open: ui.mobileOpen }">
    <div class="sidebar-brand">
      <div class="brand-mark"><img :src="logo" alt="LogiTrack" /></div>
      <div class="brand-copy"><strong>LogiTrack</strong><span>Gestión tecnológica</span></div>
      <button class="icon-btn mobile-only" @click="ui.mobileOpen = false">
        <X :size="20" />
      </button>
    </div>
    <nav class="sidebar-nav" aria-label="Navegación principal">
      <RouterLink class="nav-main" to="/dashboard" title="Dashboard" @click="ui.mobileOpen = false"
        ><CircleGauge :size="19" /><span>Dashboard</span></RouterLink
      ><template v-for="group in groups" :key="group.key"
        ><div
          v-if="grupoVisible(auth.perfilNombre, group.key) && auth.can(group.permission)"
          class="nav-group"
        >
          <button class="nav-group-button" :title="group.label" @click="toggle(group.key)">
            <component :is="group.icon" :size="19" /><span>{{ group.label }}</span
            ><ChevronDown
              class="nav-chevron"
              :class="{ rotated: opened.includes(group.key) }"
              :size="15"
            />
          </button>
          <div v-show="opened.includes(group.key) && !ui.collapsed" class="nav-submenu">
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
    <div class="sidebar-footer"><BarChart3 :size="17" /><span>LogiTrack · 2026</span></div>
  </aside>
</template>
