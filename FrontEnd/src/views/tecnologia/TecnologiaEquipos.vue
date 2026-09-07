<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import Pagination from "@/components/common/Pagination.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { aPagina, filas } from "@/services/paginacion";
import { casoService } from "@/services/casoService";
import { useUiStore } from "@/stores/uiStore";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

// Pantalla propia y no una tarjeta más: no lista envíos sino equipos con un caso abierto en
// Tecnología, con otro origen de datos y otras columnas.
const router = useRouter();
const ui = useUiStore();

const casos = ref([]);
const page = ref(1);
const pageSize = 10;
const total = ref(0);
const loading = ref(true);

const columnas = [
  { key: "equipo", label: "Equipo" },
  { key: "serial", label: "Serial" },
  { key: "ticket", label: "Ticket" },
  { key: "filial", label: "Filial dueña" },
  { key: "vueltas", label: "Movimientos" },
  { key: "antiguedad", label: "Aquí desde hace" },
];

// La antigüedad es el número que importa para operar: un equipo que lleva semanas parado no
// se distingue de uno que llegó ayer si solo se listan los tickets.
const filasCasos = computed(() =>
  casos.value.map((x) => ({
    id: x.equipoId,
    equipo: `${x.marca} ${x.modelo}`,
    serial: x.numeroSerie || x.codigoActivo || "—",
    ticket: x.numeroTicket,
    filial: x.filialNombre,
    vueltas: x.movimientos,
    antiguedad:
      x.diasAbierto === 0 ? "hoy" : `${x.diasAbierto} día${x.diasAbierto === 1 ? "" : "s"}`,
  })),
);

async function load() {
  loading.value = true;
  try {
    const pagina = await casoService.list({ page: page.value, pageSize });
    casos.value = filas(pagina);
    total.value = aPagina(pagina).totalItems;
  } catch (error) {
    casos.value = [];
    ui.notify(error.userMessage || "No fue posible cargar los equipos en Tecnología.", "error");
  } finally {
    loading.value = false;
  }
}

onMounted(load);
useRefrescoAlVolver(load);
watch(page, load);
</script>

<template>
  <div>
    <PageHeader
      title="Equipos en Tecnología"
      subtitle="Equipos con un caso abierto, con el tiempo que llevan aquí"
    >
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>

    <BaseCard title="Equipos con caso abierto" :padded="false">
      <BaseTable
        :columns="columnas"
        :rows="filasCasos"
        :loading="loading"
        @view="(row) => router.push(`/equipos/${row.id}`)"
      />
    </BaseCard>
    <Pagination :page="page" :total="total" :page-size="pageSize" @update:page="page = $event" />
  </div>
</template>
