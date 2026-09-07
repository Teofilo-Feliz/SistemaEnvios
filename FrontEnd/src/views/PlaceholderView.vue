<script setup>
import { useRoute } from "vue-router";
import { Construction } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
const route = useRoute();
</script>
<template>
  <div>
    <PageHeader
      :title="route.meta.title"
      subtitle="Módulo preparado para conectarse con la Web API"
    /><BaseCard
      ><div class="placeholder">
        <Construction :size="38" /><strong>{{
          route.params.section?.replaceAll("-", " ") || route.meta.title
        }}</strong>
        <p>La navegación, permisos y estructura visual de este módulo ya están disponibles.</p>
      </div></BaseCard
    >
  </div>
</template>
