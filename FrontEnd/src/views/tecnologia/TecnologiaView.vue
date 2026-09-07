<script setup>
import { computed, onMounted, ref, watch } from "vue";
import { RouterLink, useRouter } from "vue-router";
import {
  Bell,
  CheckCircle2,
  CircleDashed,
  ClipboardCheck,
  RefreshCw,
  Wrench,
} from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import Pagination from "@/components/common/Pagination.vue";
import { aPagina, filas } from "@/services/paginacion";
import StatCard from "@/components/dashboard/StatCard.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { envioService } from "@/services/envioService";
import { catalogoService } from "@/services/catalogoService";
import { equipoService } from "@/services/equipoService";
import { notificacionService } from "@/services/notificacionService";
import { casoService } from "@/services/casoService";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction, escapeHtml, notifyNotificationsChanged } from "@/utils/confirm";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

const router = useRouter();
const ui = useUiStore();
const loading = ref(true);
const shipments = ref([]);
const states = ref([]);
const notificationCount = ref(0);
const page = ref(1);
const pageSize = 10;
const totalItems = ref(0);
const casos = ref([]);
const casosPage = ref(1);
const casosPageSize = 10;
const casosTotal = ref(0);
const casosLoading = ref(true);
const casosColumns = [
  { key: "equipo", label: "Equipo" },
  { key: "serial", label: "Serial" },
  { key: "ticket", label: "Ticket" },
  { key: "filial", label: "Filial dueña" },
  { key: "vueltas", label: "Movimientos" },
  { key: "antiguedad", label: "Aquí desde hace" },
];
// La antigüedad es el número que importa para operar: un equipo que lleva semanas parado no
// se distingue de uno que llegó ayer si solo se listan los tickets.
const casosFilas = computed(() =>
  casos.value.map((x) => ({
    id: x.equipoId,
    equipo: `${x.marca} ${x.modelo}`,
    serial: x.numeroSerie || x.codigoActivo || "—",
    ticket: x.numeroTicket,
    filial: x.filialNombre,
    vueltas: x.movimientos,
    antiguedad:
      x.diasAbierto === 0 ? "hoy" : `${x.diasAbierto} día${x.diasAbierto === 1 ? "" : "s"}`,
  })),
);

const columns = [
  { key: "number", label: "Envío" },
  { key: "transport", label: "Transporte" },
  { key: "status", label: "Estado" },
];
const idDe = (codigo) => states.value.find((x) => x.codigo === codigo)?.estadoEnvioId;
// El flujo interno llega hasta RECIBIDO_TRANSPORTACION y desde ahí Tecnología hace la
// recepción. El privado no pasa por Transportación y conserva ESPERA_TECNOLOGIA → EN_REVISION.
const waitingId = computed(() => idDe("ESPERA_TECNOLOGIA"));
const queue = computed(() =>
  shipments.value
    .filter((x) => x.estadoEnvioId === waitingId.value)
    .map((x) => ({
      id: x.envioId,
      number: x.numeroEnvio,
      transport: x.nombreTipoTransporte || "Sin indicar",
      status: "ESPERA_TECNOLOGIA",
      canConfirm: true,
      confirmTitle: "Recibir equipos",
    })),
);
// Listos para verificar equipo por equipo y cerrar en Recibido o Recibido con incidencia.
const listosParaVerificar = computed(() => {
  const codigos = [idDe("RECIBIDO_TRANSPORTACION"), idDe("EN_REVISION")].filter(Boolean);
  return shipments.value
    .filter((x) => codigos.includes(x.estadoEnvioId))
    .map((x) => ({
      id: x.envioId,
      number: x.numeroEnvio,
      transport: x.nombreTipoTransporte || "Sin indicar",
      description: x.observaciones || "Sin descripción",
      status: x.estadoEnvioId === idDe("EN_REVISION") ? "EN_REVISION" : "RECIBIDO_TRANSPORTACION",
      canConfirm: true,
      confirmTitle: "Verificar equipos y completar recepción",
    }));
});
const inReview = listosParaVerificar;
// El ojito significa "ver detalle del envío" en toda la aplicación; la acción propia de
// esta tabla va en el botón de confirmar, igual que en la cola de retiro.
const reviewColumns = [
  { key: "number", label: "Envío" },
  { key: "description", label: "Descripción" },
  { key: "transport", label: "Transporte" },
  { key: "status", label: "Estado" },
];
const stats = computed(() => [
  { title: "Esperando Tecnología", value: queue.value.length, icon: CircleDashed, tone: "blue" },
  {
    title: "Pendientes de verificar",
    value: inReview.value.length,
    icon: ClipboardCheck,
    tone: "amber",
  },
  {
    title: "Disponibles para retirar",
    value: queue.value.length,
    icon: CheckCircle2,
    tone: "green",
  },
  { title: "Equipos en Tecnología", value: casosTotal.value, icon: Wrench, tone: "blue" },
]);

async function loadCasos() {
  casosLoading.value = true;
  try {
    const pagina = await casoService.list({ page: casosPage.value, pageSize: casosPageSize });
    casos.value = filas(pagina);
    casosTotal.value = aPagina(pagina).totalItems;
  } catch (error) {
    // Que falle la bandeja no debe tumbar las colas de envíos, que son el trabajo del día.
    casos.value = [];
    ui.notify(error.userMessage || "No fue posible cargar los equipos en Tecnología.", "error");
  } finally {
    casosLoading.value = false;
  }
}

async function load() {
  loading.value = true;
  try {
    // Solo las etapas que Tecnología atiende, paginadas en el servidor.
    const [enviosPagina, estados, notificaciones] = await Promise.all([
      envioService.paged({
        page: page.value,
        pageSize,
        estadoCodigos: ["ESPERA_TECNOLOGIA", "RECIBIDO_TRANSPORTACION", "EN_REVISION"],
      }),
      catalogoService.allStates(),
      notificacionService.listTechnology({ pageSize: 1 }),
    ]);
    shipments.value = filas(enviosPagina);
    totalItems.value = aPagina(enviosPagina).totalItems;
    states.value = estados;
    // Solo interesa cuántas hay: se pide una página de uno y se lee el total.
    notificationCount.value = aPagina(notificaciones).totalItems;
  } catch (error) {
    ui.notify(error.userMessage || "No fue posible cargar Tecnología.", "error");
  } finally {
    loading.value = false;
  }
}

async function receiveShipment(row) {
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

function cargarTodo() {
  load();
  loadCasos();
}

onMounted(cargarTodo);
// Al volver a esta pestaña los datos pueden haber cambiado en otra máquina.
useRefrescoAlVolver(cargarTodo);
watch(page, load);
watch(casosPage, loadCasos);
</script>

<template>
  <div>
    <PageHeader title="Tecnología" subtitle="Retiro desde Transportación y revisión técnica">
      <RouterLink class="btn btn-ghost" to="/tecnologia/notificaciones"
        ><Bell :size="15" /> Notificaciones
        <strong v-if="notificationCount">{{ notificationCount }}</strong></RouterLink
      >
      <button class="btn btn-ghost" :disabled="loading" @click="cargarTodo">
        <RefreshCw :size="15" /> Actualizar
      </button>
    </PageHeader>
    <div class="module-stats">
      <StatCard v-for="item in stats" :key="item.title" v-bind="item" />
    </div>
    <BaseCard title="Envíos disponibles para retirar de Transportación" :padded="false">
      <BaseTable
        :columns="columns"
        :rows="queue"
        :loading="loading"
        @confirm="receiveShipment"
        @view="(row) => router.push(`/envios/${row.id}`)"
      />
    </BaseCard>
    <BaseCard title="Listos para verificar equipos" :padded="false">
      <BaseTable
        :columns="reviewColumns"
        :rows="inReview"
        :loading="loading"
        @confirm="(row) => router.push(`/tecnologia/recepciones/${row.id}`)"
        @view="(row) => router.push(`/envios/${row.id}`)"
      />
    </BaseCard>
    <Pagination
      :page="page"
      :total="totalItems"
      :page-size="pageSize"
      @update:page="page = $event"
    />

    <BaseCard title="Equipos en Tecnología" :padded="false">
      <BaseTable
        :columns="casosColumns"
        :rows="casosFilas"
        :loading="casosLoading"
        @view="(row) => router.push(`/equipos/${row.id}`)"
      />
    </BaseCard>
    <Pagination
      v-if="casosTotal > casosPageSize"
      :page="casosPage"
      :total="casosTotal"
      :page-size="casosPageSize"
      @update:page="casosPage = $event"
    />
  </div>
</template>
