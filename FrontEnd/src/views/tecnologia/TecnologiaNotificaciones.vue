<script setup>
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { notificacionService } from "@/services/notificacionService";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction, notifyNotificationsChanged } from "@/utils/confirm";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const rows = ref([]);
const columns = [
  { key: "title", label: "Notificación" },
  { key: "shipment", label: "Envío" },
  { key: "created", label: "Fecha" },
];

async function load() {
  loading.value = true;
  try {
    const { data } = await notificacionService.listTechnology();
    rows.value = (data || []).map((item) => ({
      id: item.notificacionId,
      envioId: item.envioId,
      title: item.titulo,
      shipment: item.numeroEnvio,
      created: new Date(item.fechaCreacion).toLocaleString("es-DO"),
      canConfirm: true,
      confirmTitle: "Marcar como leída",
    }));
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar las notificaciones.", "error");
  } finally {
    loading.value = false;
  }
}

async function markRead(row) {
  if (!(await confirmAction({ title: "Marcar notificación como leída", text: `¿Deseas marcar la notificación del envío ${row.shipment} como leída?`, confirmText: "Marcar como leída" }))) return;
  try {
    await notificacionService.markRead(row.id);
    ui.notify("Notificación marcada como leída.");
    notifyNotificationsChanged();
    await load();
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible actualizar la notificación.", "error");
  }
}

onMounted(load);
</script>

<template>
  <div>
    <PageHeader title="Notificaciones de Tecnología" subtitle="Avisos de equipos disponibles para retirar de Transportación">
      <button class="btn btn-ghost" :disabled="loading" @click="load"><RefreshCw :size="15" /> Actualizar</button>
    </PageHeader>
    <BaseCard title="Notificaciones pendientes" :padded="false">
      <BaseTable :columns="columns" :rows="rows" :loading="loading" @confirm="markRead" @view="(row) => router.push(`/envios/${row.envioId}`)" />
    </BaseCard>
  </div>
</template>
