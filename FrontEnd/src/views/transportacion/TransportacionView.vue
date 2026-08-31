<script setup>
import { computed, onMounted, ref } from "vue";
import {
  CheckCircle2,
  Clock3,
  PackageCheck,
  RefreshCw,
  Truck,
} from "lucide-vue-next";
import { useRouter } from "vue-router";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import AdvancedFilter from "@/components/filters/AdvancedFilter.vue";
import DashboardKpiCard from "@/components/dashboard/DashboardKpiCard.vue";
import { filterFields } from "@/config/filterFields";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { isInternalTransport } from "@/utils/transport";
import { confirmAction, notifyNotificationsChanged } from "@/utils/confirm";
const router = useRouter(),
  ui = useUiStore(),
  loading = ref(true),
  rows = ref([]),
  locations = ref([]),
  states = ref([]),
  filters = ref({ logic: "AND", rules: [] });
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
      (x) => x.status === "PENDIENTE_CONFIRMACION_TRANSPORTE",
    ).length,
    icon: Clock3,
    tone: "amber",
  },
  {
    title: "Confirmados",
    value: rows.value.filter((x) => x.status === "CONFIRMADO_TRANSPORTACION")
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
async function load() {
  loading.value = true;
  try {
    const [e, l, s] = await Promise.all([
      envioService.list(),
      catalogoService.locations(),
      catalogoService.states(),
    ]);
    locations.value = l.data || [];
    states.value = s.data || [];
    rows.value = (e.data || []).map((x) => ({
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
      canConfirm: states.value.find((y) => y.estadoEnvioId === x.estadoEnvioId)?.codigo === "EN_TRANSITO" && isInternalTransport(x.estrategiaTransporte),
      confirmTitle: "Confirmar llegada a Tecnología",
    }));
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
}
function clear() {
  filters.value = { logic: "AND", rules: [] };
  load();
}
async function confirmArrival(row) {
  const accepted = await confirmAction({ title: "Confirmar llegada a Transportación", text: `¿Confirmas que el envío ${row.number} llegó a Transportación? Pasará a espera de Tecnología.`, confirmText: "Confirmar llegada" });
  if (!accepted) return;
  try { await envioService.confirmTransportArrival(row.id); notifyNotificationsChanged(); ui.notify("Llegada confirmada. Tecnología fue notificada."); await load(); }
  catch (e) { ui.notify(e.userMessage || "No fue posible confirmar la llegada.", "error"); }
}
onMounted(load);
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
    /><BaseCard title="Operaciones activas" :padded="false"
      ><BaseTable
        :columns="columns"
        :rows="filtered"
        :loading="loading"
        @confirm="confirmArrival"
        @view="(row) => router.push(`/envios/${row.id}`)"
    /></BaseCard>
  </div>
</template>
