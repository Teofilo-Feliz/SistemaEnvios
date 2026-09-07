<script setup>
import { onMounted, onUnmounted } from "vue";
import AppSidebar from "@/components/common/AppSidebar.vue";
import AppTopbar from "@/components/common/AppTopbar.vue";
import ToastContainer from "@/components/common/ToastContainer.vue";
import { useUiStore } from "@/stores/uiStore";

const ui = useUiStore();
function syncViewport() {
  if (window.innerWidth >= 1024) ui.mobileOpen = false;
}
onMounted(() => window.addEventListener("resize", syncViewport));
onUnmounted(() => window.removeEventListener("resize", syncViewport));
</script>

<template>
  <div class="app-shell" :class="{ 'is-collapsed': ui.collapsed }">
    <AppSidebar />
    <button
      v-if="ui.mobileOpen"
      class="sidebar-backdrop"
      aria-label="Cerrar menú"
      @click="ui.mobileOpen = false"
    />
    <div class="app-main">
      <AppTopbar />
      <main class="page-container"><RouterView /></main>
    </div>
    <ToastContainer />
  </div>
</template>
