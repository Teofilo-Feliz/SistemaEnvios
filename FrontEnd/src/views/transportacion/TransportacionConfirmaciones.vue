<script setup>
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { envioService } from "@/services/envioService";
import { transporteService } from "@/services/transporteService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { isInternalTransport } from "@/utils/transport";
import { confirmAction, notifyNotificationsChanged } from "@/utils/confirm";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const rows = ref([]);
const columns = [
  { key: "number", label: "Envío" },
  { key: "stage", label: "Confirmación pendiente" },
  { key: "type", label: "Transporte" },
  { key: "driver", label: "Chofer" },
];

async function load() {
  loading.value = true;
  try {
    const [{ data: envios }, { data: estados }] = await Promise.all([
      envioService.list(),
      catalogoService.states(),
    ]);
    const pendingId = estados.find((x) => x.codigo === "ENTREGADO_TRANSPORTACION")?.estadoEnvioId;
    const transitId = estados.find((x) => x.codigo === "EN_TRANSITO")?.estadoEnvioId;
    const items = [];
    for (const envio of envios || []) {
      if (![pendingId, transitId].includes(envio.estadoEnvioId)) continue;
      try {
        const { data: transport } = await transporteService.getByEnvio(envio.envioId);
        const isInternal = transport && isInternalTransport(transport.estrategia);
        const isCustodyPending = envio.estadoEnvioId === pendingId && !transport?.entregaConfirmada && transport?.fechaEntrega;
        const isArrivalPending = envio.estadoEnvioId === transitId && transport?.entregaConfirmada;
        if (isInternal && (isCustodyPending || isArrivalPending)) {
          items.push({
            id: transport.transporteId,
            envioId: envio.envioId,
            number: envio.numeroEnvio,
            type: transport.nombreTipo,
            driver: transport.nombreChofer || "Sin indicar",
            stage: isArrivalPending ? "Confirmar llegada a Tecnología" : "Confirmar custodia para traslado",
            action: isArrivalPending ? "arrival" : "custody",
            canConfirm: true,
            confirmTitle: isArrivalPending ? "Confirmar llegada" : "Confirmar custodia",
          });
        }
      } catch {
        // Los envíos sin transporte no pertenecen a esta bandeja.
      }
    }
    rows.value = items;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar las confirmaciones.", "error");
  } finally {
    loading.value = false;
  }
}

async function confirm(row) {
  try {
    if (row.action === "arrival") {
      await envioService.confirmTransportArrival(row.envioId);
      ui.notify("Llegada confirmada. Tecnología ha sido notificada.");
    } else {
      await transporteService.confirm(row.id);
      ui.notify("Custodia confirmada. El envío pasó a En tránsito.");
    }
    await load();
    if (row.action === "arrival") notifyNotificationsChanged();
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible completar la confirmación.", "error");
  }
}

async function requestConfirmation(row) {
  const arrival = row.action === "arrival";
  const accepted = await confirmAction({
    title: arrival ? "Confirmar llegada a Transportación" : "Confirmar recepción en Transportación",
    text: arrival
      ? `¿Confirmas que el envío ${row.number} llegó a Transportación? Pasará a espera de Tecnología.`
      : `¿Confirmas la recepción del envío ${row.number}? Pasará a En tránsito.`,
    confirmText: arrival ? "Confirmar llegada" : "Confirmar recepción",
  });
  if (accepted) await confirm(row);
}

onMounted(load);
</script>

<template>
  <div>
    <PageHeader title="Confirmaciones" subtitle="Confirma la recepción inicial y la llegada de los envíos internos">
      <button class="btn btn-ghost" :disabled="loading" @click="load">Actualizar</button>
    </PageHeader>
    <BaseCard title="Confirmaciones pendientes de Transportación" :padded="false">
      <BaseTable :columns="columns" :rows="rows" :loading="loading" @confirm="requestConfirmation" @view="(row) => router.push(`/envios/${row.envioId}`)" />
    </BaseCard>
  </div>
</template>
