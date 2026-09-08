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
import { aPagina, filas } from "@/services/paginacion";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

const router = useRouter();
const ui = useUiStore();
const auth = useAuthStore();
const loading = ref(true);
const error = ref("");
const page = ref(1);
const rows = ref([]);
const totalItems = ref(0);
const locations = ref([]);

// Solo el perfil global elige filial. A los demás el backend ya les acotó la consulta, así que
// mostrarles el selector sugeriría un control que no tienen.
const filialSeleccionada = ref("");
const filiales = computed(() => locations.value.filter((x) => x.tipo === 1 || x.tipo === "Filial"));
const alcanceTexto = computed(() => {
  if (auth.mandaEnTodo) {
    const filial = filiales.value.find(
      (x) => String(x.ubicacionId) === String(filialSeleccionada.value),
    );
    return filial ? `Envíos de ${filial.nombre}` : "Todas las filiales";
  }
  return auth.filialNombre
    ? `Envíos de ${auth.filialNombre}`
    : "Consulta y gestiona el ciclo de vida de los envíos";
});
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
  return locations.value.find((item) => item.ubicacionId === id)?.nombre || `Ubicación #${id}`;
}
function stateCode(id) {
  return states.value.find((item) => item.estadoEnvioId === id)?.codigo || "unknown";
}
function puedeEditar(item) {
  const codigo = stateCode(item.estadoEnvioId);
  if (codigo === "PREPARACION_TECNOLOGIA") return auth.mandaEnTodo;
  return codigo === "EN_FILIAL";
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
    // Editable mientras no se haya movido, en cualquiera de las dos direcciones.
    // Edita quien lo tiene en la mano: la filial lo suyo en EN_FILIAL, y lo que Tecnología
    // prepara es de Tecnología aunque la filial destino lo vea en su listado.
    canEdit: puedeEditar(item),
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
      if (
        [
          "estadoEnvioId",
          "tipoTransporteId",
          "ubicacionOrigenId",
          "ubicacionDestinoId",
          "direccion",
        ].includes(rule.field)
      )
        params[rule.field] = rule.value;
      if (rule.field === "numeroEnvio") params.search = rule.value;
    }
    if (auth.puedeFiltrarPorFilial && filialSeleccionada.value)
      params.ubicacionId = filialSeleccionada.value;
    // Los catálogos alimentan selectores, así que se recorren completos; los envíos vienen
    // de a página desde el servidor.
    const [shipments, ubicaciones, estados, tiposTransporte] = await Promise.all([
      envioService.paged(params),
      catalogoService.allLocations(),
      catalogoService.allStates(),
      catalogoService.allTransportTypes(),
    ]);
    locations.value = ubicaciones;
    states.value = estados;
    transportTypes.value = tiposTransporte;
    rows.value = filas(shipments).map(mapRow);
    totalItems.value = aPagina(shipments).totalItems;
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
// Al volver a esta pestaña los datos pueden haber cambiado en otra máquina.
useRefrescoAlVolver(load);
watch(page, load);
watch(filialSeleccionada, () => {
  page.value = 1;
  load();
});
</script>
<template>
  <div>
    <PageHeader title="Envíos" :subtitle="alcanceTexto"
      ><label v-if="auth.puedeFiltrarPorFilial" class="filial-scope"
        ><span>Filial</span>
        <select v-model="filialSeleccionada" :disabled="loading">
          <option value="">Todas las filiales</option>
          <option
            v-for="filial in filiales"
            :key="filial.ubicacionId"
            :value="String(filial.ubicacionId)"
          >
            {{ filial.nombre }}
          </option>
        </select></label
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

<style scoped>
.filial-scope {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 0.82rem;
  color: var(--text-muted, #64748b);
}
.filial-scope span {
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-weight: 600;
}
.filial-scope select {
  padding: 7px 10px;
  border: 1px solid var(--border, #d7dee6);
  border-radius: 6px;
  background: var(--surface, #fff);
  color: inherit;
  font: inherit;
  font-size: 0.86rem;
  min-width: 190px;
}
.filial-scope select:disabled {
  opacity: 0.6;
}
</style>
