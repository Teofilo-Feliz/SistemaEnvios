<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { Bell, Menu, PanelLeftClose, Search } from "lucide-vue-next";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
import { notificacionService } from "@/services/notificacionService";
import AppBreadcrumb from "./AppBreadcrumb.vue";
import NotificationDropdown from "./NotificationDropdown.vue";
import UserMenu from "./UserMenu.vue";

const ui = useUiStore();
const auth = useAuthStore();
const query = ref("");
const showNotifications = ref(false);
const showUser = ref(false);
const notifications = ref([]);
const desktopIcon = computed(() => (ui.collapsed ? Menu : PanelLeftClose));
const notificationCount = computed(() => notifications.value.length);

function toggleMenu() {
  if (window.innerWidth < 1024) ui.mobileOpen = !ui.mobileOpen;
  else ui.toggleSidebar();
}
function search() {
  if (query.value.trim()) ui.notify(`Búsqueda global: ${query.value}`, "info");
}
async function loadNotifications() {
  try {
    const { data } = await notificacionService.listTechnology();
    notifications.value = data || [];
  } catch {
    notifications.value = [];
  }
}
async function toggleNotifications() {
  showNotifications.value = !showNotifications.value;
  showUser.value = false;
  if (showNotifications.value) await loadNotifications();
}

let notificationTimer;
onMounted(() => {
  loadNotifications();
  window.addEventListener("notifications-changed", loadNotifications);
  notificationTimer = window.setInterval(loadNotifications, 30000);
});
onBeforeUnmount(() => {
  window.removeEventListener("notifications-changed", loadNotifications);
  window.clearInterval(notificationTimer);
});
</script>

<template>
  <header class="topbar">
    <div class="topbar-left"><button class="icon-btn" aria-label="Alternar menú" @click="toggleMenu"><component :is="desktopIcon" :size="21" /></button><AppBreadcrumb /></div>
    <form class="global-search" @submit.prevent="search"><Search :size="17" /><input v-model="query" placeholder="Buscar envío, ticket, serial, activo…" aria-label="Búsqueda global" /><kbd>⌘ K</kbd></form>
    <div class="topbar-actions">
      <div class="dropdown-wrap"><button class="icon-btn notification-button" aria-label="Notificaciones" @click="toggleNotifications"><Bell :size="20" /><span v-if="notificationCount">{{ notificationCount }}</span></button><NotificationDropdown v-if="showNotifications" :items="notifications" /></div>
      <div class="dropdown-wrap"><button class="user-trigger" @click="showUser = !showUser; showNotifications = false"><span class="avatar">{{ auth.user?.initials }}</span><span class="user-copy"><strong>{{ auth.user?.name }}</strong><small>{{ auth.user?.role }}</small></span></button><UserMenu v-if="showUser" /></div>
    </div>
  </header>
</template>
