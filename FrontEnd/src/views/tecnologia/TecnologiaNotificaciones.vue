<script setup>
import { onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import Pagination from "@/components/common/Pagination.vue";
import { aPagina, filas } from "@/services/paginacion";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { notificacionService } from "@/services/notificacionService";
import { useUiStore } from "@/stores/uiStore";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const rows = ref([]);
const page = ref(1);
const pageSize = 10;
const totalItems = ref(0);
const columns = [
  { key: "title", label: "Notificación" },
  { key: "message", label: "Mensaje" },
  { key: "shipment", label: "Envío" },
  { key: "created", label: "Recibida" },
];

async function load() {
  loading.value = true;
  try {
    const pagina = await notificacionService.listTechnology({ page: page.value, pageSize });
    totalItems.value = aPagina(pagina).totalItems;
    rows.value = filas(pagina).map((item) => ({
      id: item.notificacionId,
      envioId: item.envioId,
      title: item.titulo,
      shipment: item.numeroEnvio,
      created: new Date(item.fechaCreacion).toLocaleString("es-DO"),
      message: item.mensaje,
    }));
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar las notificaciones.", "error");
  } finally {
    loading.value = false;
  }
}

onMounted(load);
// Al volver a esta pestaña los datos pueden haber cambiado en otra máquina.
useRefrescoAlVolver(load);
watch(page, load);
</script>

<template>
  <div>
    <PageHeader
      title="Notificaciones de Tecnología"
      subtitle="Avisos de envíos que Transportación ya recibió y están listos para retirar"
    >
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>
    <BaseCard title="Avisos recibidos" :padded="false">
      <BaseTable
        :columns="columns"
        :rows="rows"
        :loading="loading"
        @view="(row) => router.push(`/envios/${row.envioId}`)"
      />
      <Pagination
        :page="page"
        :total="totalItems"
        :page-size="pageSize"
        @update:page="page = $event"
      />
    </BaseCard>
  </div>
</template>
