<script setup>
import { Bell } from "lucide-vue-next";

defineProps({ items: { type: Array, default: () => [] } });
</script>

<template>
  <div class="dropdown-panel notifications">
    <div class="dropdown-heading"><strong>Notificaciones</strong></div>
    <div v-if="!items.length" class="notification-item"><Bell class="info" :size="18" /><div><p>No tienes notificaciones pendientes.</p></div></div>
    <RouterLink v-for="item in items" :key="item.notificacionId" class="notification-item" :to="`/envios/${item.envioId}`">
      <Bell class="info" :size="18" />
      <div><p>{{ item.titulo }} · {{ item.numeroEnvio }}</p><small>{{ new Date(item.fechaCreacion).toLocaleString("es-DO") }}</small></div>
    </RouterLink>
    <RouterLink class="dropdown-footer" to="/tecnologia/notificaciones">Ver todas</RouterLink>
  </div>
</template>
