<script setup>
import { computed, onMounted, ref, watch } from "vue";
import {
  CheckCircle2,
  Clock3,
  PackageCheck,
  RefreshCw,
  Truck,
} from "lucide-vue-next";
import { useRouter, useRoute } from "vue-router";
import PageHeader from "@/components/common/PageHeader.vue";
import { aPagina, filas } from "@/services/paginacion";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import AdvancedFilter from "@/components/filters/AdvancedFilter.vue";
import DashboardKpiCard from "@/components/dashboard/DashboardKpiCard.vue";
import Pagination from "@/components/common/Pagination.vue";
import { filterFields } from "@/config/filterFields";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { isInternalTransport } from "@/utils/transport";
import { confirmAction, notifyNotificationsChanged } from "@/utils/confirm";
const router = useRouter(), route = useRoute(),
  ui = useUiStore(),
  loading = ref(true),
  rows = ref([]),
  locations = ref([]),
  states = ref([]),
  filters = ref({ logic: "AND", rules: [] });
const page = ref(1), pageSize = 10;
const totalItems = ref(0);
const columns = [
  { key: "number", label: "Envío" },
  { key: "origin", label: "Origen" },
  { key: "destination", label: "Destino" },
  { key: "status", label: "Estado" },
];
const sources = computed(() => ({
  states: states.value.map((x) => ({
    value: String(x.estadoEnvioId),
    label: x.nombre,
  })),
  locations: locations.value.map((x) => ({
    value: String(x.ubicacionId),
    label: x.nombre,
  })),
  directions: [
    { value: "1", label: "Filial → Tecnología" },
    { value: "2", label: "Tecnología → Filial" },
  ],
}));
const stats = computed(() => [
  {
    title: "Pendientes de confirmación",
    value: rows.value.filter(
      (x) => x.status === "ENTREGADO_TRANSPORTACION",
    ).length,
    icon: Clock3,
    tone: "amber",
  },
  {
    title: "Confirmados",
    value: rows.value.filter((x) => x.status === "EN_TRANSITO")
      .length,
    icon: PackageCheck,
    tone: "blue",
  },
  {
    title: "En tránsito",
    value: rows.value.filter((x) => x.status === "EN_TRANSITO").length,
    icon: Truck,
    tone: "indigo",
  },
  {
    title: "Recibidos",
    value: rows.value.filter((x) =>
      ["RECIBIDO_TRANSPORTACION", "RECIBIDO_FILIAL"].includes(x.status),
    ).length,
    icon: CheckCircle2,
    tone: "green",
  },
]);
function compare(v, op, e) {
  const a = String(v ?? "").toLowerCase(),
    b = String(e ?? "").toLowerCase();
  if (op === "equals") return a === b;
  if (op === "notEquals") return a !== b;
  if (op === "contains") return a.includes(b);
  if (op === "notContains") return !a.includes(b);
  if (op === "startsWith") return a.startsWith(b);
  if (op === "endsWith") return a.endsWith(b);
  if (op === "empty") return !a;
  if (op === "notEmpty") return !!a;
  return true;
}
function match(r, g) {
  const a = g.rules.map((x) =>
    x.type === "group"
      ? match(r, x)
      : x.type === "global"
        ? compare(Object.values(r).join(" "), x.operator, x.value)
        : (!x.value && !["empty", "notEmpty"].includes(x.operator)) ||
          compare(r[x.field], x.operator, x.value),
  );
  return g.logic === "OR" ? a.some(Boolean) : a.every(Boolean);
}
const filtered = computed(() =>
  filters.value.rules?.length
    ? rows.value.filter((x) => match(x, filters.value))
    : rows.value,
);
// La página ya viene cortada del servidor; aquí solo se separan las dos tablas.
const assignmentRows = computed(() => filtered.value.filter(x => x.status === "TRANSPORTE_ASIGNADO"));
const otherRows = computed(() => filtered.value.filter(x => x.status !== "TRANSPORTE_ASIGNADO"));
const ETAPAS_TRANSPORTACION = [
  "ENTREGADO_TRANSPORTACION", "EN_TRANSITO", "RECIBIDO_TRANSPORTACION",
  "INCIDENCIA_TRANSPORTACION", "DESPACHADO_TECNOLOGIA", "EN_TRANSPORTACION",
  "TRANSPORTE_ASIGNADO", "DESPACHADO_TRANSPORTACION",
];
async function load() {
  loading.value = true;
  try {
    // Solo las etapas que custodia Transportación, paginadas en el servidor.
    const [e, l, s] = await Promise.all([
      envioService.paged({
        page: page.value,
        pageSize,
        estadoCodigos: ETAPAS_TRANSPORTACION,
        estadoEnvioId: undefined,
      }),
      catalogoService.allLocations(),
      catalogoService.allStates(),
    ]);
    locations.value = l;
    states.value = s;
    totalItems.value = aPagina(e).totalItems;
    rows.value = filas(e).map((x) => ({
      id: x.envioId,
      number: x.numeroEnvio,
      origin:
        locations.value.find((y) => y.ubicacionId === x.ubicacionOrigenId)
          ?.nombre || `Ubicación #${x.ubicacionOrigenId}`,
      destination:
        locations.value.find((y) => y.ubicacionId === x.ubicacionDestinoId)
          ?.nombre || `Ubicación #${x.ubicacionDestinoId}`,
      status:
        states.value.find((y) => y.estadoEnvioId === x.estadoEnvioId)?.codigo ||
        "",
      estadoEnvioId: x.estadoEnvioId,
      ubicacionOrigenId: x.ubicacionOrigenId,
      ubicacionDestinoId: x.ubicacionDestinoId,
      numeroEnvio: x.numeroEnvio,
      direccion: String(x.direccion),
      canConfirm: ["TRANSPORTE_ASIGNADO", "EN_TRANSITO"].includes(states.value.find((y) => y.estadoEnvioId === x.estadoEnvioId)?.codigo) && Boolean(x.estrategiaTransporte),
      confirmTitle: states.value.find((y) => y.estadoEnvioId === x.estadoEnvioId)?.codigo === "TRANSPORTE_ASIGNADO" ? "Despachado por transportación" : "Confirmar llegada a Tecnología",
    }));
    if (route.query.estado) filters.value = { logic: "AND", rules: [{ field: "status", operator: "equals", value: String(route.query.estado) }] };
  } catch (e) {
    ui.notify(
      e.userMessage || "No fue posible cargar transportación.",
      "error",
    );
  } finally {
    loading.value = false;
  }
}
function search(x) {
  filters.value = x;
  page.value = 1;
  load();
}
function clear() {
  filters.value = { logic: "AND", rules: [] };
  page.value = 1;
  load();
}
async function confirmArrival(row) {
  if (row.status === "TRANSPORTE_ASIGNADO") {
    const accepted = await confirmAction({ title: "Despachar envío a filial", text: `¿Confirmas el despacho del envío ${row.number}?`, confirmText: "Despachar" });
    if (!accepted) return;
    try { await envioService.dispatchFromTechnology(row.id); ui.notify("Envío despachado y notificado a la filial.", "success"); await load(); } catch (e) { ui.notify(e.userMessage || "No fue posible despachar el envío.", "error"); }
    return;
  }
  const accepted = await confirmAction({ title: "Confirmar llegada a Transportación", text: `¿Confirmas que el envío ${row.number} llegó a Transportación? Pasará a espera de Tecnología.`, confirmText: "Confirmar llegada" });
  if (!accepted) return;
  try { await envioService.confirmTransportArrival(row.id); notifyNotificationsChanged(); ui.notify("Llegada confirmada. Tecnología fue notificada."); await load(); }
  catch (e) { ui.notify(e.userMessage || "No fue posible confirmar la llegada.", "error"); }
}
onMounted(load);
// Cambiar de página vuelve a consultar: el corte lo hace el servidor, no el navegador.
watch(page, load);
</script>
<template>
  <div>
    <PageHeader
      title="Transportación"
      subtitle="Control de confirmaciones, tránsito y recepciones"
      ><button class="btn btn-ghost" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button></PageHeader
    >
    <div class="module-stats">
      <DashboardKpiCard v-for="stat in stats" :key="stat.title" v-bind="stat" />
    </div>
    <AdvancedFilter
      :fields="filterFields"
      :sources="sources"
      :loading="loading"
      @search="search"
      @clear="clear"
    /><BaseCard title="Despacho por Transportación — listos para salir" :padded="false"
      ><BaseTable
        :columns="columns"
        :rows="assignmentRows"
        :loading="loading"
        @confirm="confirmArrival"
        @view="(row) => router.push(`/envios/${row.id}`)"
    /><div class="transport-section-hint">Los envíos con chofer asignado aparecen aquí. Usa el botón de acción para marcarlos como despachados.</div></BaseCard>
    <BaseCard title="Seguimiento y confirmaciones" :padded="false"><BaseTable :columns="columns" :rows="otherRows" :loading="loading" @confirm="confirmArrival" @view="(row) => router.push(`/envios/${row.id}`)" /><Pagination :page="page" :total="totalItems" :page-size="pageSize" @update:page="page = $event" /></BaseCard>
  </div>
</template>
