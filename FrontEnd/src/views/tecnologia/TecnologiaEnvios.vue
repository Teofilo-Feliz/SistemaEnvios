<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { RouterLink, useRoute, useRouter } from "vue-router";
import { Bell, RefreshCw } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import Pagination from "@/components/common/Pagination.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { aPagina, filas } from "@/services/paginacion";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { equipoService } from "@/services/equipoService";
import { notificacionService } from "@/services/notificacionService";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction, escapeHtml, notifyNotificationsChanged } from "@/utils/confirm";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";
import { vistaTecnologia } from "@/config/vistasTecnologia";

// Las tres pantallas de envíos comparten componente: cambian los estados que consultan y qué
// hace el botón de confirmar, no el trabajo de cargar, paginar y dibujar. La vista la define
// la ruta, no un parámetro de consulta, para que el menú marque la activa sin ambigüedad.
const route = useRoute();
const router = useRouter();
const ui = useUiStore();

const vista = computed(() => vistaTecnologia(route.meta.vista));

const loading = ref(true);
const shipments = ref([]);
const states = ref([]);
const notificationCount = ref(0);
const page = ref(1);
const pageSize = 10;
const totalItems = ref(0);

const idDe = (codigo) => states.value.find((x) => x.codigo === codigo)?.estadoEnvioId;
const codigoDe = (id) => states.value.find((x) => x.estadoEnvioId === id)?.codigo || "";

const filasEnvios = computed(() =>
  shipments.value.map((x) => ({
    id: x.envioId,
    number: x.numeroEnvio,
    transport: x.nombreTipoTransporte || "Sin indicar",
    description: x.observaciones || "Sin descripción",
    status: codigoDe(x.estadoEnvioId),
    canConfirm: Boolean(vista.value?.accion),
    confirmTitle: vista.value?.textoAccion,
  })),
);

async function load() {
  if (!vista.value) return;
  loading.value = true;
  try {
    const [enviosPagina, estados, notificaciones] = await Promise.all([
      // Solo los estados de esta pantalla: así el total y la paginación son los suyos y no los
      // de un montón mezclado.
      envioService.paged({ page: page.value, pageSize, estadoCodigos: vista.value.estados }),
      catalogoService.allStates(),
      notificacionService.listTechnology({ pageSize: 1 }),
    ]);
    shipments.value = filas(enviosPagina);
    totalItems.value = aPagina(enviosPagina).totalItems;
    states.value = estados;
    notificationCount.value = aPagina(notificaciones).totalItems;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar Tecnología.", "error");
  } finally {
    loading.value = false;
  }
}

async function recibirEnvio(row) {
  try {
    const associations = filas(await envioService.equipment(row.id, { pageSize: 100 }));
    const equipment = await Promise.all(
      associations.map(async (association) => {
        const { data } = await equipoService.get(association.equipoId);
        return {
          ...data,
          ticket: association.numeroTicket,
          observations: association.observaciones,
        };
      }),
    );
    const details = equipment.length
      ? `<div style="text-align:left;max-height:330px;overflow:auto">${equipment
          .map(
            (item) =>
              `<div style="border:1px solid #e1e6ee;border-radius:6px;padding:10px;margin:8px 0"><strong>${escapeHtml(item.marca)} ${escapeHtml(item.modelo)}</strong><br><small>Serial: ${escapeHtml(item.numeroSerie)} · Activo: ${escapeHtml(item.codigoActivo)} · Ticket: ${escapeHtml(item.ticket)}</small><br><small>${escapeHtml(item.observations)}</small></div>`,
          )
          .join("")}</div>`
      : "<p>Este envío no tiene equipos asociados.</p>";
    const confirmed = await confirmAction({
      title: `Recibir envío ${row.number}`,
      html: `<p>Verifica los equipos antes de confirmar la recepción:</p>${details}`,
      confirmText: "Confirmar recepción",
    });
    if (!confirmed) return;
    const reviewState = states.value.find((state) => state.codigo === "EN_REVISION");
    if (!reviewState) throw new Error("El estado EN_REVISION no está configurado.");
    await envioService.changeState(row.id, {
      estadoDestinoId: reviewState.estadoEnvioId,
      observaciones: "Equipos recibidos y verificados por Tecnología.",
    });
    ui.notify("Recepción confirmada. El envío pasó a revisión.", "success");
    notifyNotificationsChanged();
    await load();
  } catch (error) {
    ui.notify(error.userMessage || error.message || "No fue posible recibir el envío.", "error");
  }
}

function confirmar(row) {
  if (vista.value?.accion === "recibir") return recibirEnvio(row);
  if (vista.value?.accion === "verificar") return router.push(`/tecnologia/recepciones/${row.id}`);
}

onMounted(load);
useRefrescoAlVolver(load);
watch(page, load);
// Al pasar de una pantalla a otra cambia la ruta pero no el componente: sin esto la lista se
// quedaría con los envíos de la pantalla anterior.
watch(
  () => route.meta.vista,
  () => {
    page.value = 1;
    load();
  },
);
</script>

<template>
  <div v-if="vista">
    <PageHeader :title="vista.titulo" :subtitle="vista.subtitulo">
      <RouterLink class="btn btn-ghost" to="/tecnologia/notificaciones">
        <Bell :size="15" /> Notificaciones
        <strong v-if="notificationCount">{{ notificationCount }}</strong>
      </RouterLink>
      <button class="btn btn-ghost" :disabled="loading" @click="load">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>

    <BaseCard :title="vista.subtitulo" :padded="false">
      <BaseTable
        :columns="vista.columnas"
        :rows="filasEnvios"
        :loading="loading"
        @confirm="confirmar"
        @view="(row) => router.push(`/envios/${row.id}`)"
      />
    </BaseCard>
    <Pagination
      :page="page"
      :total="totalItems"
      :page-size="pageSize"
      @update:page="page = $event"
    />
  </div>
</template>
