<script setup>
import { computed, onMounted, ref } from "vue";
import { PackageCheck, Send, Truck, UserRound, Warehouse, RefreshCw } from "lucide-vue-next";
import { useRouter } from "vue-router";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import "@/assets/styles/transport-dashboard.css";
import TransportationStageCard from "@/components/transportation/TransportationStageCard.vue";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const shipments = ref([]);
const states = ref([]);

const codeOf = (x) => states.value.find((s) => s.estadoEnvioId === x.estadoEnvioId)?.codigo;
// La dirección viaja como texto ("HaciaTecnologia") o como número según el serializador.
const haciaTecnologia = (x) =>
  x.direccion === 1 || String(x.direccion).toLowerCase() === "haciatecnologia";

function contar(codigo, filtroDireccion) {
  return shipments.value.filter(
    (x) => codeOf(x) === codigo && (!filtroDireccion || filtroDireccion(x)),
  ).length;
}

// Solo etapas que Transportación custodia. RECIBIDO_FILIAL quedó fuera a propósito: ese
// estado ya no está en su alcance, así que la tarjeta marcaba cero siempre.
const flujoDesdeFilial = computed(() => [
  { code: "ENTREGADO_TRANSPORTACION", title: "Entregados por la filial", icon: PackageCheck, value: contar("ENTREGADO_TRANSPORTACION") },
  { code: "EN_TRANSITO", title: "En tránsito a Tecnología", icon: Truck, value: contar("EN_TRANSITO", haciaTecnologia) },
  { code: "RECIBIDO_TRANSPORTACION", title: "En punto logístico", icon: Warehouse, value: contar("RECIBIDO_TRANSPORTACION") },
]);

const flujoDesdeTecnologia = computed(() => [
  { code: "EN_TRANSPORTACION", title: "Recibidos de Tecnología", icon: PackageCheck, value: contar("EN_TRANSPORTACION") },
  { code: "TRANSPORTE_ASIGNADO", title: "Chofer asignado", icon: UserRound, value: contar("TRANSPORTE_ASIGNADO") },
  { code: "EN_TRANSITO", title: "En tránsito a la filial", icon: Send, value: contar("EN_TRANSITO", (x) => !haciaTecnologia(x)) },
]);

// El listado llega ordenado por fecha de creación descendente: los primeros son los recientes.
const recientes = computed(() =>
  shipments.value.slice(0, 5).map((x) => ({
    number: x.numeroEnvio,
    status: codeOf(x),
    transport: x.nombreTipoTransporte || "Sin transporte",
  })),
);

// Envíos detenidos esperando una acción de Transportación. Sin fechas en el listado no se
// puede medir demora real, así que se nombra por lo que es: pendientes, no "críticos".
const pendientes = computed(() =>
  ["ENTREGADO_TRANSPORTACION", "EN_TRANSPORTACION", "TRANSPORTE_ASIGNADO"].reduce(
    (total, codigo) => total + contar(codigo),
    0,
  ),
);

const porTipoTransporte = computed(() => {
  const conteo = new Map();
  for (const envio of shipments.value) {
    const nombre = envio.nombreTipoTransporte || "Sin transporte";
    conteo.set(nombre, (conteo.get(nombre) || 0) + 1);
  }
  return [...conteo.entries()].sort((a, b) => b[1] - a[1]);
});

async function load() {
  loading.value = true;
  try {
    const [envios, estados] = await Promise.all([envioService.list(), catalogoService.states()]);
    shipments.value = envios.data || [];
    states.value = estados.data || [];
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
