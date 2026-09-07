<script setup>
import { Inbox } from "lucide-vue-next";
defineProps({
  title: { type: String, default: "No hay resultados" },
  text: { type: String, default: "Ajusta los filtros o registra nueva información." },
});
</script>
<template>
  <div class="empty-state">
    <Inbox :size="34" /><strong>{{ title }}</strong>
    <p>{{ text }}</p>
    <slot />
  </div>
</template>
