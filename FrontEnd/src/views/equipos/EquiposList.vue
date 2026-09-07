<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { Plus, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import Pagination from "@/components/common/Pagination.vue";
import ModalCard from "@/components/common/ModalCard.vue";
import EquipoInfoCard from "@/components/equipos/EquipoInfoCard.vue";
import AdvancedFilter from "@/components/filters/AdvancedFilter.vue";
import { equipmentFilterFields } from "@/config/equipmentFilterFields";
import { equipoService } from "@/services/equipoService";
import { catalogoService } from "@/services/catalogoService";
import { aPagina, filas } from "@/services/paginacion";
import { useUiStore } from "@/stores/uiStore";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const equipment = ref([]);
const locations = ref([]);
const types = ref([]);
const filters = ref({ logic: "AND", rules: [] });
const page = ref(1);
const pageSize = 10;
const totalItems = ref(0);

const columns = [
  { key: "type", label: "Equipo" },
  { key: "brandModel", label: "Marca / Modelo" },
  { key: "serial", label: "Serial" },
  { key: "asset", label: "Código activo" },
  { key: "location", label: "Ubicación" },
];

const sources = computed(() => ({
  locations: locations.value.map((x) => ({ value: String(x.ubicacionId), label: x.nombre })),
  types: types.value.map((x) => ({ value: String(x.tipoEquipoId), label: x.nombre })),
}));

const rows = computed(() =>
  equipment.value.map((x) => ({
    ...x,
    id: x.equipoId,
    type:
      types.value.find((t) => t.tipoEquipoId === x.tipoEquipoId)?.nombre ||
      `Tipo #${x.tipoEquipoId}`,
    brandModel: `${x.marca} ${x.modelo}`,
    serial: x.numeroSerie || "—",
    asset: x.codigoActivo || "—",
    location:
      locations.value.find((l) => l.ubicacionId === x.ubicacionActualId)?.nombre ||
      `Ubicación #${x.ubicacionActualId}`,
  })),
);

// El filtro se traduce a parámetros de la consulta en vez de recorrer una lista ya traída: el
// inventario son 34 filiales por todos sus equipos y no cabe en el navegador.
function parametros() {
  const params = { page: page.value, pageSize };
  const textos = [];
  for (const rule of filters.value.rules || []) {
    if (rule.type === "global" && rule.value) textos.push(rule.value);
    if (rule.type !== "rule" || !rule.value) continue;
    if (rule.field === "tipoEquipoId") params.tipoEquipoId = rule.value;
    else if (rule.field === "ubicacionActualId") params.ubicacionActualId = rule.value;
    else if (["numeroSerie", "codigoActivo", "marca", "modelo"].includes(rule.field))
      textos.push(rule.value);
  }
  if (textos.length) params.search = textos[0];
  return params;
}

async function load() {
  loading.value = true;
  try {
    const [equipos, ubicaciones, tipos] = await Promise.all([
      equipoService.list(parametros()),
      catalogoService.allLocations(),
      catalogoService.allTypes(),
    ]);
    equipment.value = filas(equipos);
    totalItems.value = aPagina(equipos).totalItems;
    locations.value = ubicaciones;
    types.value = tipos;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar los equipos.", "error");
  } finally {
    loading.value = false;
  }
}

function search(value) {
  filters.value = value;
  page.value = 1;
  load();
}
function clear() {
  filters.value = { logic: "AND", rules: [] };
  page.value = 1;
  load();
}
onMounted(load);
watch(page, load);

// La página ya trae todos los campos del equipo, así que el popup no necesita otra consulta.
const selectedId = ref(null);
const selected = computed(
  () => equipment.value.find((x) => x.equipoId === selectedId.value) || null,
);
const selectedType = computed(
  () =>
    types.value.find((x) => x.tipoEquipoId === selected.value?.tipoEquipoId)?.nombre || "Equipo",
);
</script>

<template>
  <div>
    <PageHeader title="Equipos" subtitle="Equipos registrados y asociados a envíos">
      <RouterLink class="btn btn-primary" to="/equipos/nuevo"
        ><Plus :size="16" /> Nuevo equipo</RouterLink
      >
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="16" /> Actualizar
      </button>
    </PageHeader>
    <AdvancedFilter
      :fields="equipmentFilterFields"
      :sources="sources"
      :loading="loading"
      @search="search"
      @clear="clear"
    />
    <BaseCard :padded="false">
      <div v-if="loading" class="page-loading">Cargando equipos…</div>
      <BaseTable v-else :columns="columns" :rows="rows" @view="(row) => (selectedId = row.id)" />
      <Pagination
        :page="page"
        :total="totalItems"
        :page-size="pageSize"
        @update:page="page = $event"
      />
    </BaseCard>
    <ModalCard
      :open="Boolean(selected)"
      :eyebrow="selectedType"
      :title="selected ? `${selected.marca} ${selected.modelo}` : ''"
      @close="selectedId = null"
    >
      <EquipoInfoCard v-if="selected" :equipo="selected" :types="types" :locations="locations" />
      <template #footer>
        <button
          class="btn btn-ghost"
          type="button"
          @click="router.push(`/equipos/${selected.equipoId}`)"
        >
          Abrir ficha completa
        </button>
      </template>
    </ModalCard>
  </div>
</template>
