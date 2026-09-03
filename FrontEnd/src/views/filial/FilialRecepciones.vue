<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import PageHeader from "@/components/common/PageHeader.vue";
import Pagination from "@/components/common/Pagination.vue";
import { aPagina, filas } from "@/services/paginacion";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";

const router = useRouter();
const ui = useUiStore();
const auth = useAuthStore();
const loading = ref(true);
const shipments = ref([]);
const states = ref([]);
const page = ref(1);
const pageSize = 10;
const totalItems = ref(0);

const columns = [
  { key: "numeroEnvio", label: "Envío" },
  { key: "descripcion", label: "Descripción" },
  { key: "estadoEnvioNombre", label: "Estado" },
];

const esHaciaFilial = (x) =>
  x.direccion === 2 || String(x.direccion).toLowerCase() === "haciafilial";

const pending = computed(() => {
  const porId = Object.fromEntries(states.value.map((x) => [x.estadoEnvioId, x]));
  return shipments.value
    .filter(
      (x) =>
        ["EN_TRANSITO", "PENDIENTE_RECEPCION_FILIAL", "RECIBIDO_FILIAL"].includes(
          porId[x.estadoEnvioId]?.codigo,
        ) && esHaciaFilial(x),
    )
    .map((x) => ({
      id: x.envioId,
      envioId: x.envioId,
      numeroEnvio: x.numeroEnvio,
      descripcion: x.observaciones || "Sin descripción",
      estadoEnvioCodigo: porId[x.estadoEnvioId]?.codigo,
      estadoEnvioNombre: porId[x.estadoEnvioId]?.nombre || porId[x.estadoEnvioId]?.codigo,
      canConfirm: true,
      confirmTitle: "Verificar equipos y completar recepción",
    }));
});

const subtitulo = computed(() =>
  auth.filialNombre
    ? `Equipos que Tecnología envió a ${auth.filialNombre}`
    : "Confirma los equipos enviados por Tecnología",
);

async function load() {
  loading.value = true;
  try {
    const [enviosPagina, estados] = await Promise.all([
      envioService.paged({
        page: page.value,
        pageSize,
        estadoCodigos: ['EN_TRANSITO', 'PENDIENTE_RECEPCION_FILIAL', 'RECIBIDO_FILIAL'],
      }),
      catalogoService.allStates(),
    ]);
    shipments.value = filas(enviosPagina);
    totalItems.value = aPagina(enviosPagina).totalItems;
    states.value = estados;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar recepciones.", "error");
  } finally {
    loading.value = false;
  }
}

// La llegada se registra aquí; la verificación equipo por equipo ocurre en la pantalla de
// recepción, la misma que usa Tecnología. Antes esta vista marcaba todo como conforme, así
// que la filial nunca podía reportar un equipo dañado.
async function recibir(row) {
  try {
    if (row.estadoEnvioCodigo === "EN_TRANSITO") {
      await envioService.registerFilialArrival(row.envioId);
    }
    router.push(`/filial/recepciones/${row.envioId}`);
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible registrar la llegada.", "error");
  }
}

onMounted(load);
watch(page, load);
</script>

<template>
  <div>
    <PageHeader title="Recepciones de filial" :subtitle="subtitulo">
      <button class="btn btn-secondary" :disabled="loading" @click="load">Actualizar</button>
    </PageHeader>
    <BaseCard title="Envíos pendientes de recepción" :padded="false">
      <BaseTable
        :columns="columns"
        :rows="pending"
        :loading="loading"
        @confirm="recibir"
        @view="(row) => router.push(`/envios/${row.envioId}`)"
      />
      <Pagination :page="page" :total="totalItems" :page-size="pageSize" @update:page="page = $event" />
    </BaseCard>
  </div>
</template>
