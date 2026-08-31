<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { Plus, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import Pagination from "@/components/common/Pagination.vue";
import AdvancedFilter from "@/components/filters/AdvancedFilter.vue";
import { filterFields } from "@/config/filterFields";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const error = ref("");
const page = ref(1);
const rows = ref([]);
const totalItems = ref(0);
const locations = ref([]);
const states = ref([]);
const transportTypes = ref([]);
const activeFilters = ref({ logic: "AND", rules: [] });
const columns = [
  { key: "number", label: "Número", sortable: true },
  { key: "origin", label: "Origen", sortable: true },
  { key: "destination", label: "Destino", sortable: true },
  { key: "transport", label: "Transporte", sortable: true },
  { key: "equipment", label: "Equipos" },
  { key: "status", label: "Estado", sortable: true },
  { key: "created", label: "Fecha creación", sortable: true },
  { key: "dispatched", label: "Fecha despacho" },
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
  transportTypes: transportTypes.value.map((x) => ({
    value: String(x.tipoTransporteId),
    label: x.nombre,
  })),
  directions: [
    { value: "1", label: "Filial → Tecnología" },
    { value: "2", label: "Tecnología → Filial" },
  ],
}));
function locationName(id) {
  return (
    locations.value.find((item) => item.ubicacionId === id)?.nombre ||
    `Ubicación #${id}`
  );
}
function stateCode(id) {
  return (
    states.value.find((item) => item.estadoEnvioId === id)?.codigo || "unknown"
  );
}
function mapRow(item) {
  return {
    id: item.envioId,
    number: item.numeroEnvio,
    origin: locationName(item.ubicacionOrigenId),
    destination: locationName(item.ubicacionDestinoId),
    transport: item.nombreTipoTransporte || "Sin transporte",
    equipment: "—",
    status: stateCode(item.estadoEnvioId),
    created: "—",
    dispatched: "—",
    envioId: item.envioId,
    numeroEnvio: item.numeroEnvio,
    estadoEnvioId: item.estadoEnvioId,
    ubicacionOrigenId: item.ubicacionOrigenId,
    ubicacionDestinoId: item.ubicacionDestinoId,
    direccion: String(item.direccion),
    tipoTransporteId: item.tipoTransporteId == null ? "" : String(item.tipoTransporteId),
    observaciones: item.observaciones || "",
    canEdit: stateCode(item.estadoEnvioId) === "EN_FILIAL",
  };
}
function compare(value, operator, expected) {
  const left = String(value ?? "").toLowerCase();
  const right = String(expected ?? "").toLowerCase();
  if (operator === "equals") return left === right;
  if (operator === "notEquals") return left !== right;
  if (operator === "contains") return left.includes(right);
  if (operator === "notContains") return !left.includes(right);
  if (operator === "startsWith") return left.startsWith(right);
  if (operator === "endsWith") return left.endsWith(right);
  if (operator === "empty") return !left;
  if (operator === "notEmpty") return Boolean(left);
  const n = Number(value);
  const e = Number(expected);
  return operator === "greaterThan"
    ? n > e
    : operator === "greaterOrEqual"
      ? n >= e
      : operator === "lessThan"
        ? n < e
        : operator === "lessOrEqual"
          ? n <= e
          : true;
}
function evaluateRule(row, rule) {
  if (rule.type === "group") return matches(row, rule);
  if (rule.type === "global")
    return compare(Object.values(row).join(" "), rule.operator, rule.value);
  return (
    (!rule.value && !["empty", "notEmpty"].includes(rule.operator)) ||
    compare(row[rule.field], rule.operator, rule.value)
  );
}
function matches(row, group) {
  const results = group.rules.map((rule) => evaluateRule(row, rule));
  return group.logic === "OR" ? results.some(Boolean) : results.every(Boolean);
}
const filtered = computed(() => rows.value);
async function load() {
  loading.value = true;
  error.value = "";
  try {
    const params = { page: page.value, pageSize: 10 };
    for (const rule of activeFilters.value.rules || []) {
      if (rule.type !== "rule" || rule.operator !== "equals" || !rule.value) continue;
      if (["estadoEnvioId", "tipoTransporteId", "ubicacionOrigenId", "ubicacionDestinoId", "direccion"].includes(rule.field)) params[rule.field] = rule.value;
      if (rule.field === "numeroEnvio") params.search = rule.value;
    }
    const [shipments, locationResponse, stateResponse, transportResponse] = await Promise.all([
      envioService.paged(params),
      catalogoService.locations(),
      catalogoService.states(),
      catalogoService.transportTypes(),
    ]);
    locations.value = locationResponse.data || [];
    states.value = stateResponse.data || [];
    transportTypes.value = transportResponse.data || [];
    rows.value = (shipments.data?.items || []).map(mapRow);
    totalItems.value = shipments.data?.totalItems || 0;
  } catch (exception) {
    error.value = exception.userMessage || "No fue posible cargar los envíos.";
    ui.notify(error.value, "error");
  } finally {
    loading.value = false;
  }
}
function search(filters) {
  activeFilters.value = filters;
  page.value = 1;
  load();
}
function clear() {
  activeFilters.value = { logic: "AND", rules: [] };
  page.value = 1;
  load();
}
onMounted(load);
watch(page, load);
</script>
<template>
  <div>
    <PageHeader
      title="Envíos"
      subtitle="Consulta y gestiona el ciclo de vida de los envíos"
      ><button class="btn btn-secondary" :disabled="loading" @click="load">
        <RefreshCw :size="16" /> Actualizar</button
      ><RouterLink class="btn btn-primary" to="/envios/nuevo"
        ><Plus :size="17" /> Nuevo envío</RouterLink
      ></PageHeader
    ><AdvancedFilter
      :fields="filterFields"
      :sources="sources"
      :loading="loading"
      @search="search"
      @clear="clear"
    /><BaseCard v-if="error" class="error-banner">{{ error }}</BaseCard
    ><BaseCard :padded="false"
      ><BaseTable
        :columns="columns"
        :rows="filtered"
        :loading="loading"
        editable
        @view="(row) => router.push(`/envios/${row.id}`)"
        @edit="(row) => router.push(`/envios/${row.id}/editar`)" /><Pagination
        v-if="!loading"
        v-model:page="page"
        :total="totalItems"
        :page-size="10"
    /></BaseCard>
  </div>
</template>
