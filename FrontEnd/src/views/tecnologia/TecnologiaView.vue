<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink, useRouter } from "vue-router";
import { Bell, CheckCircle2, CircleDashed, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import StatCard from "@/components/dashboard/StatCard.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { equipoService } from "@/services/equipoService";
import { notificacionService } from "@/services/notificacionService";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction, escapeHtml, notifyNotificationsChanged } from "@/utils/confirm";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const shipments = ref([]);
const states = ref([]);
const notificationCount = ref(0);
const columns = [
  { key: "number", label: "Envío" },
  { key: "transport", label: "Transporte" },
  { key: "status", label: "Estado" },
];
const waitingId = computed(() => states.value.find((x) => x.codigo === "ESPERA_TECNOLOGIA")?.estadoEnvioId);
const queue = computed(() => shipments.value.filter((x) => x.estadoEnvioId === waitingId.value).map((x) => ({
  id: x.envioId,
  number: x.numeroEnvio,
  transport: x.nombreTipoTransporte || "Sin indicar",
  status: "ESPERA_TECNOLOGIA",
  canConfirm: true,
  confirmTitle: "Recibir equipos",
})));
const stats = computed(() => [
  { title: "Esperando Tecnología", value: queue.value.length, icon: CircleDashed, tone: "blue" },
  { title: "Disponibles para retirar", value: queue.value.length, icon: CheckCircle2, tone: "green" },
]);

async function load() {
  loading.value = true;
  try {
    const [{ data: envios }, { data: estados }, { data: notifications }] = await Promise.all([
      envioService.list(),
      catalogoService.states(),
      notificacionService.listTechnology(),
    ]);
    shipments.value = envios || [];
    states.value = estados || [];
    notificationCount.value = (notifications || []).length;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar Tecnología.", "error");
  } finally {
    loading.value = false;
  }
}

async function receiveShipment(row) {
  try {
    const { data: associations } = await envioService.equipment(row.id);
    const equipment = await Promise.all((associations || []).map(async (association) => {
      const { data } = await equipoService.get(association.equipoId);
      return { ...data, ticket: association.numeroTicket, observations: association.observaciones };
    }));
    const details = equipment.length
      ? `<div style="text-align:left;max-height:330px;overflow:auto">${equipment.map((item) => `<div style="border:1px solid #e1e6ee;border-radius:6px;padding:10px;margin:8px 0"><strong>${escapeHtml(item.marca)} ${escapeHtml(item.modelo)}</strong><br><small>Serial: ${escapeHtml(item.numeroSerie)} · Activo: ${escapeHtml(item.codigoActivo)} · Ticket: ${escapeHtml(item.ticket)}</small><br><small>${escapeHtml(item.observations)}</small></div>`).join("")}</div>`
      : "<p>Este envío no tiene equipos asociados.</p>";
    const confirmed = await confirmAction({
      title: `Recibir envío ${row.number}`,
      html: `<p>Verifica los equipos antes de confirmar la recepción:</p>${details}`,
      confirmText: "Confirmar recepción",
    });
    if (!confirmed) return;
    const reviewState = states.value.find((state) => state.codigo === "EN_REVISION");
    if (!reviewState) throw new Error("El estado EN_REVISION no está configurado.");
    await envioService.changeState(row.id, { estadoDestinoId: reviewState.estadoEnvioId, observaciones: "Equipos recibidos y verificados por Tecnología." });
    ui.notify("Recepción confirmada. El envío pasó a revisión.", "success");
    notifyNotificationsChanged();
    await load();
  } catch (error) {
    ui.notify(error.userMessage || error.message || "No fue posible recibir el envío.", "error");
  }
}

onMounted(load);
</script>

<template>
  <div>
    <PageHeader title="Tecnología" subtitle="Retiro desde Transportación y revisión técnica">
      <RouterLink class="btn btn-ghost" to="/tecnologia/notificaciones"><Bell :size="15" /> Notificaciones <strong v-if="notificationCount">{{ notificationCount }}</strong></RouterLink>
      <button class="btn btn-ghost" :disabled="loading" @click="load"><RefreshCw :size="15" /> Actualizar</button>
    </PageHeader>
    <div class="module-stats"><StatCard v-for="item in stats" :key="item.title" v-bind="item" /></div>
    <BaseCard title="Envíos disponibles para retirar de Transportación" :padded="false">
      <BaseTable :columns="columns" :rows="queue" :loading="loading" @confirm="receiveShipment" @view="(row) => router.push(`/envios/${row.id}`)" />
    </BaseCard>
  </div>
</template>
