<script setup>
import { computed } from 'vue'; import { useRoute } from 'vue-router'; import { ChevronRight, Home } from 'lucide-vue-next'
const route = useRoute(); const title = computed(() => route.meta.title || 'Inicio')
</script>
<template><nav class="breadcrumb" aria-label="Ruta"><RouterLink to="/dashboard"><Home :size="14" /><span class="sr-only">Inicio</span></RouterLink><ChevronRight :size="14" /><span>{{ title }}</span></nav></template>
