<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ArrowLeft, RefreshCw, Ticket } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import LoadingSpinner from "@/components/common/LoadingSpinner.vue";
import Pagination from "@/components/common/Pagination.vue";
import EquipoInfoCard from "@/components/equipos/EquipoInfoCard.vue";
import { equipoService } from "@/services/equipoService";
import { catalogoService } from "@/services/catalogoService";
import { aPagina, filas } from "@/services/paginacion";
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

// El historial se pagina aparte de la ficha: un equipo con años de vida acumula viajes, y
// cambiar de página no debería recargar los datos del equipo.
const viajes = ref([]);
const viajesPage = ref(1);
const viajesPageSize = 10;
const viajesTotal = ref(0);
const viajesLoading = ref(true);

const tipoNombre = computed(
  () =>
    types.value.find((x) => x.tipoEquipoId === equipo.value?.tipoEquipoId)?.nombre ||
    "Detalle del equipo",
);
const titulo = computed(() =>
  equipo.value ? `${equipo.value.marca} ${equipo.value.modelo}` : "Equipo",
);

const fecha = (valor) =>
  valor ? new Date(valor).toLocaleDateString("es-DO", { day: "2-digit", month: "short", year: "numeric" }) : "—";

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

async function loadViajes() {
  viajesLoading.value = true;
  try {
    const pagina = await equipoService.history(equipoId, {
      page: viajesPage.value,
      pageSize: viajesPageSize,
    });
    viajes.value = filas(pagina);
    viajesTotal.value = aPagina(pagina).totalItems;
  } catch (exception) {
    // El historial no debe tumbar la ficha: si falla, la información del equipo sigue visible.
    viajes.value = [];
    ui.notify(exception.userMessage || "No fue posible cargar el historial de viajes.", "error");
  } finally {
    viajesLoading.value = false;
  }
}

onMounted(() => {
  load();
  loadViajes();
});
watch(viajesPage, loadViajes);
</script>

<template>
  <div>
    <PageHeader :title="titulo" :subtitle="tipoNombre">
      <button class="btn btn-ghost" type="button" @click="router.push('/equipos')">
        <ArrowLeft :size="15" /> Volver a equipos
      </button>
      <button class="btn btn-ghost" type="button" :disabled="loading" @click="() => { load(); loadViajes(); }">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>

    <BaseCard v-if="error" class="error-banner">{{ error }}</BaseCard>
    <LoadingSpinner v-else-if="loading" />

    <template v-else-if="equipo">
      <BaseCard title="Información del equipo">
        <EquipoInfoCard :equipo="equipo" :types="types" :locations="locations" />
      </BaseCard>

      <BaseCard title="Historial de viajes">
        <LoadingSpinner v-if="viajesLoading" />
        <div v-else-if="!viajes.length" class="empty-state">
          Este equipo todavía no ha viajado.
        </div>
        <ol v-else class="viajes">
          <li v-for="viaje in viajes" :key="viaje.envioId">
            <div class="viaje-linea">
              <RouterLink class="viaje-envio" :to="`/envios/${viaje.envioId}`">
                {{ viaje.numeroEnvio }}
              </RouterLink>
              <span class="viaje-fecha">{{ fecha(viaje.fecha) }}</span>
            </div>
            <div class="viaje-ruta">
              {{ viaje.origen }} <span aria-hidden="true">→</span> {{ viaje.destino }}
            </div>
            <div class="viaje-pie">
              <span class="viaje-estado">{{ viaje.estadoNombre }}</span>
              <!-- Los dos viajes de un mismo caso muestran el mismo ticket. Sin decir cuál lo
                   abrió, el número repetido parece un error en vez de una continuación. -->
              <span class="viaje-ticket" :class="{ heredado: !viaje.esAperturaDeCaso }">
                <Ticket :size="12" /> {{ viaje.numeroTicket }}
                <em>{{ viaje.esAperturaDeCaso ? "abre el caso" : "hereda el ticket" }}</em>
              </span>
            </div>
            <p v-if="viaje.observaciones" class="viaje-nota">{{ viaje.observaciones }}</p>
          </li>
        </ol>
        <Pagination
          v-if="viajesTotal > viajesPageSize"
          :page="viajesPage"
          :total="viajesTotal"
          :page-size="viajesPageSize"
          @update:page="viajesPage = $event"
        />
      </BaseCard>
    </template>
  </div>
</template>

<style scoped>
.viajes {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.viajes li {
  border: 1px solid var(--border, #e1e6ee);
  border-radius: 8px;
  padding: 11px 13px;
}
.viaje-linea {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 10px;
}
.viaje-envio {
  font-weight: 700;
  text-decoration: none;
  color: inherit;
}
.viaje-envio:hover {
  text-decoration: underline;
}
.viaje-fecha {
  font-size: 12px;
  opacity: 0.7;
  white-space: nowrap;
}
.viaje-ruta {
  margin-top: 3px;
  font-size: 13px;
}
.viaje-pie {
  margin-top: 7px;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}
.viaje-estado,
.viaje-ticket {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  border-radius: 999px;
  padding: 2px 9px;
  font-size: 11px;
  font-weight: 600;
}
.viaje-estado {
  background: #eef2f8;
  color: #33415c;
}
/* El que abre el caso se distingue del que lo continúa. */
.viaje-ticket {
  background: #e7f2ea;
  color: #1f6b3f;
}
.viaje-ticket.heredado {
  background: #f2eee7;
  color: #7a5c1e;
}
.viaje-ticket em {
  font-style: normal;
  font-weight: 500;
  opacity: 0.85;
}
.viaje-nota {
  margin: 7px 0 0;
  font-size: 12px;
  opacity: 0.8;
}
</style>
