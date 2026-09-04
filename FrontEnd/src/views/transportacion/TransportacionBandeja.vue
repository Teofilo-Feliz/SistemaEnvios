<script setup>
/**
 * Transportación confirma dos cosas distintas y en momentos distintos. Estaban en una sola
 * pantalla y se prestaba a confusión, así que ahora son dos bandejas con la misma mecánica:
 *
 *   custodia  el chofer recibió el equipo y sale a ruta   → el envío pasa a En tránsito
 *   llegada   el envío llegó a Transportación             → Tecnología puede retirarlo
 *
 * Comparten componente porque el trabajo es idéntico; lo que cambia es qué se confirma.
 */
import { computed, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import Pagination from "@/components/common/Pagination.vue";
import { transporteService } from "@/services/transporteService";
import { envioService } from "@/services/envioService";
import { aPagina, filas } from "@/services/paginacion";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction, notifyNotificationsChanged } from "@/utils/confirm";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

const props = defineProps({ modo: { type: String, required: true } });

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const rows = ref([]);
const page = ref(1);
const pageSize = 10;
const totalItems = ref(0);

const TEXTOS = {
  custodia: {
    titulo: "Recepción del chofer",
    subtitulo: "Envíos entregados a Transportación que esperan la confirmación del chofer",
    tabla: "Pendientes de que el chofer confirme",
    vacio: "Ningún envío espera confirmación del chofer.",
    accion: "Confirmar que el chofer recibió",
    dialogo: (numero) =>
      `¿Confirmas que el chofer recibió el envío ${numero}? El envío pasará a En tránsito.`,
    exito: "Recepción del chofer confirmada. El envío salió a ruta.",
  },
  llegada: {
    titulo: "Llegada a Transportación",
    subtitulo: "Envíos en ruta que esperan la confirmación de su llegada",
    tabla: "Pendientes de confirmar llegada",
    vacio: "Ningún envío está pendiente de confirmar llegada.",
    accion: "Confirmar llegada",
    dialogo: (numero) =>
      `¿Confirmas que el envío ${numero} llegó a Transportación? Tecnología recibirá el aviso para retirarlo.`,
    exito: "Llegada confirmada. Tecnología recibió el aviso.",
  },
};

const texto = computed(() => TEXTOS[props.modo]);

const columns = [
  { key: "number", label: "Envío" },
  { key: "route", label: "Recorrido" },
  { key: "driver", label: "Chofer" },
  { key: "type", label: "Transporte" },
  { key: "since", label: "Entregado" },
];

async function load() {
  loading.value = true;
  try {
    const consulta = props.modo === "custodia"
      ? transporteService.pendingCustody
      : transporteService.pendingArrival;
    const pagina = await consulta({ page: page.value, pageSize });
    totalItems.value = aPagina(pagina).totalItems;
    rows.value = filas(pagina).map((item) => ({
      id: item.transporteId,
      envioId: item.envioId,
      transporteId: item.transporteId,
      number: item.numeroEnvio,
      route: `${item.ubicacionOrigen} → ${item.ubicacionDestino}`,
      driver: `${item.nombreChofer} · ${item.numeroEmpleadoChofer}`,
      type: item.nombreTipoTransporte,
      since: item.fechaEntrega ? new Date(item.fechaEntrega).toLocaleString("es-DO") : "—",
      canConfirm: true,
      confirmTitle: texto.value.accion,
    }));
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar la bandeja.", "error");
  } finally {
    loading.value = false;
  }
}

async function confirmar(row) {
  const aceptado = await confirmAction({
    title: texto.value.accion,
    text: texto.value.dialogo(row.number),
    confirmText: texto.value.accion,
  });
  if (!aceptado) return;
  try {
    if (props.modo === "custodia") {
      await transporteService.confirm(row.transporteId);
    } else {
      await envioService.confirmTransportArrival(row.envioId);
      notifyNotificationsChanged();
    }
    ui.notify(texto.value.exito, "success");
    await load();
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible completar la confirmación.", "error");
  }
}

onMounted(load);
// Al volver a esta pestaña los datos pueden haber cambiado en otra máquina.
useRefrescoAlVolver(load);
watch(page, load);
// Las dos bandejas comparten componente: al cambiar de una a otra hay que volver a empezar.
watch(() => props.modo, () => { page.value = 1; load(); });
</script>

<template>
  <div>
    <PageHeader :title="texto.titulo" :subtitle="texto.subtitulo">
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>
    <BaseCard :title="texto.tabla" :padded="false">
      <div v-if="loading" class="page-loading">Cargando…</div>
      <div v-else-if="!rows.length" class="empty-state">{{ texto.vacio }}</div>
      <BaseTable
        v-else
        :columns="columns"
        :rows="rows"
        @confirm="confirmar"
        @view="(row) => router.push(`/envios/${row.envioId}`)"
      />
      <Pagination :page="page" :total="totalItems" :page-size="pageSize" @update:page="page = $event" />
    </BaseCard>
  </div>
</template>
