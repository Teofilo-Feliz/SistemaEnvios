<script setup>
import { computed, onMounted, ref } from "vue";
import { PackageCheck, Send, Truck, UserRound, Warehouse, RefreshCw } from "lucide-vue-next";
import { useRouter } from "vue-router";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import { envioService } from "@/services/envioService";
import { dashboardService } from "@/services/dashboardService";
import { filas } from "@/services/paginacion";
import { useUiStore } from "@/stores/uiStore";
import "@/assets/styles/transport-dashboard.css";
import TransportationStageCard from "@/components/transportation/TransportationStageCard.vue";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
// Los totales los cuenta la base y llegan ya sumados: la pantalla no recibe envíos para
// contarlos por su cuenta, que era lo que obligaba a descargar la tabla completa.
const totales = ref({ porEtapa: [], porTipoTransporte: [], totalEnEtapas: 0 });
const shipments = ref([]);

const HACIA_TECNOLOGIA = 1;
function contar(codigo, direccion) {
  return totales.value.porEtapa
    .filter((x) => x.codigo === codigo && (direccion === undefined || x.direccion === direccion))
    .reduce((total, x) => total + x.total, 0);
}

// Solo etapas que Transportación custodia. RECIBIDO_FILIAL quedó fuera a propósito: ese
// estado ya no está en su alcance, así que la tarjeta marcaba cero siempre.
const flujoDesdeFilial = computed(() => [
  { code: "ENTREGADO_TRANSPORTACION", title: "Entregados por la filial", icon: PackageCheck, value: contar("ENTREGADO_TRANSPORTACION") },
  { code: "EN_TRANSITO", title: "En tránsito a Tecnología", icon: Truck, value: contar("EN_TRANSITO", HACIA_TECNOLOGIA) },
  { code: "RECIBIDO_TRANSPORTACION", title: "En punto logístico", icon: Warehouse, value: contar("RECIBIDO_TRANSPORTACION") },
]);

const flujoDesdeTecnologia = computed(() => [
  // Es la bandeja de trabajo de Transportación: lo que está parado esperando un chofer.
  { code: "EN_TRANSPORTACION", title: "Esperan asignación de chofer", icon: UserRound, value: contar("EN_TRANSPORTACION") },
  { code: "EN_TRANSITO", title: "En tránsito a la filial", icon: Send, value: contar("EN_TRANSITO", 2) },
]);

// El listado llega ordenado por fecha de creación descendente: los primeros son los recientes.
const recientes = computed(() =>
  shipments.value.map((x) => ({
    number: x.numeroEnvio,
    status: x.estadoCodigo,
    transport: x.nombreTipoTransporte || "Sin transporte",
  })),
);

// Envíos detenidos esperando una acción de Transportación. Sin fechas en el listado no se
// puede medir demora real, así que se nombra por lo que es: pendientes, no "críticos".
const pendientes = computed(() =>
  ["ENTREGADO_TRANSPORTACION", "EN_TRANSPORTACION"].reduce(
    (total, codigo) => total + contar(codigo),
    0,
  ),
);

const porTipoTransporte = computed(() =>
  totales.value.porTipoTransporte.map((x) => [x.label, x.total]),
);

const ETAPAS_TRANSPORTACION = [
  "ENTREGADO_TRANSPORTACION", "EN_TRANSITO", "RECIBIDO_TRANSPORTACION",
  "INCIDENCIA_TRANSPORTACION", "DESPACHADO_TECNOLOGIA", "EN_TRANSPORTACION",
];
async function load() {
  loading.value = true;
  try {
    // Un agregado para los totales y una página de cinco para la lista de recientes.
    const [resumen, recientesPagina] = await Promise.all([
      dashboardService.transportacion(),
      envioService.paged({ pageSize: 5, estadoCodigos: ETAPAS_TRANSPORTACION }),
    ]);
    totales.value = resumen.data || { porEtapa: [], porTipoTransporte: [], totalEnEtapas: 0 };
    shipments.value = filas(recientesPagina);
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar el dashboard.", "error");
  } finally {
    loading.value = false;
  }
}

function open(stage) {
  router.push({ path: "/transportacion/operaciones", query: { estado: stage.code } });
}

onMounted(load);
// Al volver a esta pestaña los datos pueden haber cambiado en otra máquina.
useRefrescoAlVolver(load);
</script>

<template>
  <div class="dashboard-page transport-dashboard">
    <PageHeader title="Dashboard de Transportación" subtitle="Operación organizada por etapas">
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Sincronizar datos
      </button>
    </PageHeader>

    <BaseCard title="Filial → Tecnología" :padded="false">
      <div class="transport-stage-grid">
        <TransportationStageCard
          v-for="stage in flujoDesdeFilial"
          :key="`f-${stage.code}`"
          :title="stage.title"
          :count="stage.value"
          :icon="stage.icon"
          :active="stage.value > 0"
          @view="open(stage)"
        />
      </div>
    </BaseCard>

    <BaseCard title="Tecnología → Filial" :padded="false">
      <div class="transport-stage-grid">
        <TransportationStageCard
          v-for="stage in flujoDesdeTecnologia"
          :key="`t-${stage.code}`"
          :title="stage.title"
          :count="stage.value"
          :icon="stage.icon"
          :active="stage.value > 0"
          @view="open(stage)"
        />
      </div>
    </BaseCard>

    <div class="transport-mini-grid">
      <div class="transport-mini-card">
        <h4>Envíos recientes</h4>
        <div v-for="item in recientes" :key="item.number" class="mini-row">
          <span>{{ item.number }}</span><small>{{ item.status }}</small>
        </div>
        <p v-if="!recientes.length">Sin envíos registrados.</p>
      </div>

      <div class="transport-mini-card">
        <h4>Por tipo de transporte</h4>
        <div v-for="[nombre, total] in porTipoTransporte" :key="nombre" class="mini-row">
          <span>{{ nombre }}</span><small>{{ total }}</small>
        </div>
        <p v-if="!porTipoTransporte.length">Sin envíos registrados.</p>
      </div>

      <div class="transport-mini-card">
        <h4>Pendientes de acción</h4>
        <div v-if="pendientes" class="pending-alert">
          {{ pendientes }} envío{{ pendientes === 1 ? "" : "s" }} esperando a Transportación
        </div>
        <p v-else>Nada pendiente por ahora.</p>
        <RouterLink class="transport-open-button" to="/transportacion/operaciones">
          Abrir centro de operaciones
        </RouterLink>
      </div>
    </div>
  </div>
</template>
