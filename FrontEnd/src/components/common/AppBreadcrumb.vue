<script setup>
import { computed } from "vue";
import { useRoute } from "vue-router";
import { ChevronRight, Home } from "lucide-vue-next";
import { migaDeRuta } from "@/config/menu";

const route = useRoute();

/**
 * Los tramos de la ruta actual, del menú lateral.
 *
 * Antes esto leía route.meta.title, que solo definían 4 de las 46 rutas, así que la miga decía
 * "Inicio" en casi todo el sistema. El menú ya sabe cómo se llama cada pantalla y en qué módulo
 * está; usarlo evita repetir cuarenta y dos etiquetas en el router y que las dos se separen.
 */
const tramos = computed(() => {
  const encontrados = migaDeRuta(route.path, route.meta.title || "");
  return encontrados.length ? encontrados : ["Inicio"];
});
</script>
<template>
  <nav class="breadcrumb" aria-label="Ruta">
    <RouterLink to="/dashboard"><Home :size="14" /><span class="sr-only">Inicio</span></RouterLink
    ><template v-for="(tramo, i) in tramos" :key="tramo + i"
      ><ChevronRight :size="14" /><span :class="{ actual: i === tramos.length - 1 }">{{
        tramo
      }}</span></template
    >
  </nav>
</template>

<style scoped>
/* Los tramos intermedios son contexto; el último es dónde está el usuario. */
.breadcrumb span:not(.actual) {
  opacity: 0.72;
}
</style>
