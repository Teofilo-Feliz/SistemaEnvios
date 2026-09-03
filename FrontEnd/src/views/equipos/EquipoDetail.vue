<script setup>
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ArrowLeft, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import LoadingSpinner from "@/components/common/LoadingSpinner.vue";
import EquipoInfoCard from "@/components/equipos/EquipoInfoCard.vue";
import { equipoService } from "@/services/equipoService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";

const route = useRoute();
const router = useRouter();
const ui = useUiStore();

const loading = ref(true);
const error = ref("");
const equipo = ref(null);
const locations = ref([]);
const types = ref([]);
const equipoId = Number(route.params.id);

const tipoNombre = computed(
  () =>
    types.value.find((x) => x.tipoEquipoId === equipo.value?.tipoEquipoId)?.nombre ||
    "Detalle del equipo",
);
const titulo = computed(() =>
  equipo.value ? `${equipo.value.marca} ${equipo.value.modelo}` : "Equipo",
);

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const [{ data: item }, ubicaciones, tipos] = await Promise.all([
      equipoService.get(equipoId),
      catalogoService.allLocations(),
      catalogoService.allTypes(),
    ]);
    equipo.value = item;
    locations.value = ubicaciones;
    types.value = tipos;
  } catch (exception) {
    error.value = exception.userMessage || "No fue posible cargar el equipo.";
    ui.notify(error.value, "error");
  } finally {
    loading.value = false;
  }
}

onMounted(load);
</script>

<template>
  <div>
    <PageHeader :title="titulo" :subtitle="tipoNombre">
      <button class="btn btn-ghost" type="button" @click="router.push('/equipos')">
        <ArrowLeft :size="15" /> Volver a equipos
      </button>
      <button class="btn btn-ghost" type="button" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>

    <BaseCard v-if="error" class="error-banner">{{ error }}</BaseCard>
    <LoadingSpinner v-else-if="loading" />

    <BaseCard v-else-if="equipo" title="Información del equipo">
      <EquipoInfoCard :equipo="equipo" :types="types" :locations="locations" />
    </BaseCard>
  </div>
</template>
