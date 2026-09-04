<script setup>
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import {
  AlertTriangle, ClipboardList, Laptop, PackageCheck, RefreshCw, Send, Truck,
} from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import StatCard from "@/components/dashboard/StatCard.vue";
import { dashboardService } from "@/services/dashboardService";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

const router = useRouter();
const ui = useUiStore();
const auth = useAuthStore();
const loading = ref(true);
const datos = ref({ filialNombre: "", porEtapa: [], equiposEnFilial: 0, equiposFuera: 0, casoMasAntiguoEnDias: 0 });

const HACIA_TECNOLOGIA = 1;
const HACIA_FILIAL = 2;

// Los totales llegan por etapa y dirección; aquí se agrupan por lo que significan para la
// filial, que es lo único que le importa: qué tengo por mandar, qué va en camino, qué me llega.
function contar(codigos, direccion) {
  return datos.value.porEtapa
    .filter((x) => codigos.includes(x.codigo) && (direccion === undefined || x.direccion === direccion))
    .reduce((total, x) => total + x.total, 0);
}

const porSalir = computed(() => contar(["EN_FILIAL"]));
const enCamino = computed(() =>
  contar(["ENTREGADO_TRANSPORTACION", "DESPACHADO_TRANSPORTE_PRIVADO", "RECIBIDO_TRANSPORTACION"]) +
  contar(["EN_TRANSITO"], HACIA_TECNOLOGIA));
const enTecnologia = computed(() => contar(["ESPERA_TECNOLOGIA", "EN_REVISION"]));
const porRecibir = computed(() => contar(["EN_TRANSITO"], HACIA_FILIAL));

const tarjetas = computed(() => [
  { title: "Por entregar a Transportación", value: porSalir.value, icon: ClipboardList, tone: "amber", ruta: "/envios?estado=EN_FILIAL" },
  { title: "En camino a Tecnología", value: enCamino.value, icon: Truck, tone: "blue", ruta: "/envios" },
  { title: "En Tecnología", value: enTecnologia.value, icon: Send, tone: "blue", ruta: "/envios" },
  { title: "Por recibir en la filial", value: porRecibir.value, icon: PackageCheck, tone: "green", ruta: "/filial/recepciones" },
]);

const subtitulo = computed(() =>
  datos.value.filialNombre
    ? `Operación de ${datos.value.filialNombre}`
    : auth.filialNombre || "Operación de su filial",
);

// Un caso lleva días abierto cuando el equipo salió y no ha vuelto. Es la señal de que algo
// se quedó parado en Tecnología, y es lo único del tablero que pide una acción concreta.
const alerta = computed(() =>
  datos.value.equiposFuera > 0 && datos.value.casoMasAntiguoEnDias >= 15
    ? `Hay ${datos.value.equiposFuera} equipo${datos.value.equiposFuera === 1 ? "" : "s"} fuera de la filial, y el más antiguo lleva ${datos.value.casoMasAntiguoEnDias} días sin volver.`
    : "",
);

async function load() {
  loading.value = true;
  try {
    const { data } = await dashboardService.filial();
    datos.value = data;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar el tablero de la filial.", "error");
  } finally {
    loading.value = false;
  }
}

onMounted(load);
// Al volver a esta pestaña los datos pueden haber cambiado en otra máquina.
useRefrescoAlVolver(load);
</script>

<template>
  <div>
    <PageHeader title="Mi filial" :subtitle="subtitulo">
      <RouterLink class="btn btn-primary" to="/envios/nuevo">Nuevo envío</RouterLink>
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>

    <div v-if="loading" class="page-loading">Cargando el tablero…</div>

    <template v-else>
      <BaseCard v-if="alerta" class="filial-alerta">
        <p><AlertTriangle :size="16" /> {{ alerta }}</p>
      </BaseCard>

      <div class="module-stats">
        <StatCard
          v-for="tarjeta in tarjetas"
          :key="tarjeta.title"
          :title="tarjeta.title"
          :value="tarjeta.value"
          :icon="tarjeta.icon"
          :tone="tarjeta.tone"
          @click="router.push(tarjeta.ruta)"
        />
      </div>

      <div class="filial-inventario">
        <BaseCard title="Equipos de la filial">
          <div class="inventario-grid">
            <div>
              <Laptop :size="18" />
              <strong>{{ datos.equiposEnFilial }}</strong>
              <span>en la filial</span>
            </div>
            <div>
              <Send :size="18" />
              <strong>{{ datos.equiposFuera }}</strong>
              <span>fuera, con caso abierto</span>
            </div>
          </div>
          <p class="inventario-nota">
            Un equipo cuenta como fuera desde que sale hasta que vuelve y la filial lo recibe
            conforme. Mientras tanto conserva su número de ticket.
          </p>
        </BaseCard>
      </div>
    </template>
  </div>
</template>

<style scoped>
.filial-alerta {
  margin-bottom: 12px;
  border-left: 3px solid #b4533f;
}
.filial-alerta p {
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0;
  color: #7d3c2d;
  line-height: 1.5;
}
.filial-inventario {
  margin-top: 12px;
}
.inventario-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}
.inventario-grid > div {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 14px;
  border: 1px solid var(--border);
  border-radius: 7px;
}
.inventario-grid svg {
  color: var(--primary);
}
.inventario-grid strong {
  font-size: 22px;
  color: var(--ink);
  font-variant-numeric: tabular-nums;
}
.inventario-grid span {
  color: var(--muted);
  font-size: 10px;
}
.inventario-nota {
  margin: 14px 0 0;
  color: var(--muted);
  line-height: 1.55;
}
</style>
