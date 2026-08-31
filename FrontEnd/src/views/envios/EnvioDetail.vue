<script setup>
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import {
  ArrowLeft,
  MapPin,
  Package,
  Pencil,
  Printer,
  RefreshCw,
} from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import BaseCard from "@/components/common/BaseCard.vue";
import StatusBadge from "@/components/common/StatusBadge.vue";
import ShipmentTimeline from "@/components/envios/ShipmentTimeline.vue";
import BaseTable from "@/components/common/BaseTable.vue";
import { envioService } from "@/services/envioService";
import { transporteService } from "@/services/transporteService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
import { isInternalTransport, isPrivateTransport } from "@/utils/transport";
import { confirmAction, notifyNotificationsChanged } from "@/utils/confirm";

const route = useRoute();
const router = useRouter();
const ui = useUiStore();
const auth = useAuthStore();
const deliveryOpen = ref(false);
const delivering = ref(false);
const confirmingTransport = ref(false);
const registeringArrival = ref(false);
const tab = ref("summary");
const loading = ref(true);
const shipment = ref(null);
const locations = ref([]);
const states = ref([]);
const equipment = ref([]);
const history = ref([]);
const transport = ref(null);
const reception = ref(null);
const incidents = ref([]);
const tabs = [
  ["summary", "Resumen"],
  ["equipment", "Equipos"],
  ["transport", "Transportación"],
  ["reception", "Recepción"],
  ["history", "Historial"],
  ["incidents", "Incidencias"],
];
const equipCols = [
  { key: "equipment", label: "Equipo" },
  { key: "ticket", label: "Ticket" },
  { key: "observations", label: "Observaciones" },
];
const locationName = (id) =>
  locations.value.find((x) => x.ubicacionId === id)?.nombre ||
  `Ubicación #${id}`;
const stateCode = (id) =>
  states.value.find((x) => x.estadoEnvioId === id)?.codigo || "";
const stateName = (id) =>
  states.value.find((x) => x.estadoEnvioId === id)?.nombre || stateCode(id);
const flowLabel = computed(() =>
  shipment.value?.direccion === 2
    ? "Tecnología → Filial"
    : "Filial → Tecnología",
);
const events = computed(() =>
  history.value.map((x) => ({
    label: stateName(x.estadoEnvioId),
    date: new Date(x.fecha).toLocaleString("es-DO"),
    actor: x.usuarioId,
    current: x.estadoEnvioId === shipment.value?.estadoEnvioId,
  })),
);
const mappedEquipment = computed(() =>
  equipment.value.map((x) => ({
    equipment: `Equipo #${x.equipoId}`,
    ticket: x.numeroTicket || "—",
    observations: x.observaciones || "—",
  })),
);
async function load() {
  loading.value = true;
  try {
    const requests = [
      envioService.get(route.params.id),
      catalogoService.locations(),
      catalogoService.states(),
      envioService.equipment(route.params.id),
      envioService.history(route.params.id),
      transporteService.getByEnvio(route.params.id),
      envioService.reception(route.params.id),
      envioService.incidents(route.params.id),
    ];
    const [
      detail,
      locationResult,
      stateResult,
      equipmentResult,
      historyResult,
      transportResult,
      receptionResult,
      incidentsResult,
    ] = await Promise.all(
      requests.map((request) => request.catch(() => ({ data: null }))),
    );
    shipment.value = detail.data;
    locations.value = locationResult.data || [];
    states.value = stateResult.data || [];
    equipment.value = equipmentResult.data || [];
    history.value = historyResult.data || [];
    transport.value = transportResult.data;
    reception.value = receptionResult.data;
    incidents.value = incidentsResult.data || [];
  } catch (error) {
    ui.showToast(
      error.userMessage || "No fue posible cargar el envío.",
      "error",
    );
  } finally {
    loading.value = false;
  }
}
async function confirmTransport() {
  if (
    !transport.value?.transporteId ||
    stateCode(shipment.value?.estadoEnvioId) !==
      "PENDIENTE_CONFIRMACION_TRANSPORTE" ||
    transport.value.entregaConfirmada
  )
    return;
  if (!(await confirmAction({ title: "Confirmar recepción en Transportación", text: `¿Confirmas la recepción del envío ${shipment.value.numeroEnvio}? Pasará a En tránsito.`, confirmText: "Confirmar recepción" }))) return;
  confirmingTransport.value = true;
  try {
    await transporteService.confirm(transport.value.transporteId);
    ui.showToast("Transportación confirmada.", "success");
    await load();
  } catch (error) {
    ui.showToast(
      error.userMessage || "No fue posible confirmar la transportación.",
      "error",
    );
  } finally {
    confirmingTransport.value = false;
  }
}
async function requestDelivery() {
  const privado = isPrivateTransport(transport.value?.estrategia);
  const accepted = await confirmAction({
    title: privado ? "Entregar a transporte privado" : "Entregar a Transportación",
    text: privado
      ? "¿Confirmas la entrega al responsable privado? El envío pasará a En tránsito."
      : "¿Confirmas la entrega física al chofer interno? Quedará pendiente de confirmación por Transportación.",
    confirmText: "Confirmar entrega",
  });
  if (accepted) await deliverToTransport();
}
async function deliverToTransport() {
  deliveryOpen.value = false;
  delivering.value = true;
  try {
    const privado = isPrivateTransport(transport.value?.estrategia);
    await (privado
      ? envioService.deliverPrivate(route.params.id)
      : envioService.deliverToTransport(route.params.id));
    ui.showToast(
      privado
        ? "Entrega privada registrada. El envío quedó en tránsito."
        : "Entrega registrada. El envío quedó pendiente de confirmación de transportación.",
      "success",
    );
    await load();
  } catch (error) {
    ui.showToast(
      error.userMessage || "No fue posible registrar la entrega.",
      "error",
    );
  } finally {
    delivering.value = false;
  }
}
async function registerTechnologyArrival() {
  const interno=isInternalTransport(transport.value?.estrategia);
  const accepted = await confirmAction({ title: interno ? "Confirmar llegada a Transportación" : "Confirmar recepción privada", text: `¿Deseas confirmar la llegada del envío ${shipment.value.numeroEnvio}?`, confirmText: "Confirmar llegada" });
  if (!accepted) return;
  registeringArrival.value = true;
  try {
    await (interno?envioService.confirmTransportArrival(route.params.id):envioService.registerTechnologyArrival(route.params.id));
    if (interno) notifyNotificationsChanged();
    ui.showToast(interno?"Llegada confirmada. Tecnología fue notificada.":"Recepción privada registrada.", "success");
    await load();
  } catch (error) {
    ui.showToast(error.userMessage || "No fue posible registrar la llegada.", "error");
  } finally { registeringArrival.value = false; }
}
function printPage() {
  window.print();
}
onMounted(load);
</script>
<template>
  <button
    v-if="
      !loading &&
      shipment &&
      transport &&
      auth.can('envios.despachar') &&
      stateCode(shipment.estadoEnvioId) === 'EN_FILIAL'
    "
    class="btn btn-primary shipment-delivery-action"
    :disabled="delivering"
    type="button"
    @click="requestDelivery"
  >
    {{
      delivering
        ? "Procesando…"
        : isPrivateTransport(transport.estrategia)
          ? "Entregado a transporte privado"
          : "Entregado a transportación"
    }}
  </button>
  <div v-if="loading" class="page-loading">Cargando envío…</div>
  <div v-else-if="shipment">
    <PageHeader
      :title="`Envío ${shipment.numeroEnvio}`"
      subtitle="Seguimiento integral y trazabilidad del envío"
      ><button class="btn btn-secondary" @click="router.back()">
        <ArrowLeft :size="16" /> Volver</button
      ><button v-if="stateCode(shipment.estadoEnvioId) === 'EN_FILIAL' && auth.can('envios.editar')" class="btn btn-primary" @click="router.push(`/envios/${shipment.envioId}/editar`)">
        <Pencil :size="16" /> Editar envío</button
      ><button class="btn btn-secondary" @click="printPage">
        <Printer :size="16" /> Imprimir</button
      ><button class="btn btn-ghost" @click="load">
        <RefreshCw :size="16" /> Actualizar
      </button></PageHeader
    >
    <BaseCard class="shipment-hero"
      ><div class="shipment-title">
        <div>
          <span class="eyebrow">Estado actual</span
          ><StatusBadge :status="stateCode(shipment.estadoEnvioId)" />
        </div>
        <span class="shipment-code">{{ shipment.numeroEnvio }}</span>
      </div>
      <div class="shipment-facts">
        <div>
          <MapPin :size="18" /><span
            ><small>Origen</small
            ><strong>{{
              locationName(shipment.ubicacionOrigenId)
            }}</strong></span
          >
        </div>
        <div>
          <MapPin :size="18" /><span
            ><small>Destino</small
            ><strong>{{
              locationName(shipment.ubicacionDestinoId)
            }}</strong></span
          >
        </div>
        <div>
          <span
            ><small>Dirección</small><strong>{{ flowLabel }}</strong></span
          >
        </div>
        <div>
          <Package :size="18" /><span
            ><small>Cantidad equipos</small
            ><strong>{{ equipment.length }}</strong></span
          >
        </div>
      </div></BaseCard
    >
    <nav class="detail-tabs">
      <button
        v-for="item in tabs"
        :key="item[0]"
        :class="{ active: tab === item[0] }"
        @click="tab = item[0]"
      >
        {{ item[1] }}
      </button>
    </nav>
    <div v-if="tab === 'summary'" class="detail-grid">
      <BaseCard title="Progreso del envío"
        ><ShipmentTimeline :events="events" /></BaseCard
      ><BaseCard title="Información logística"
        ><dl class="summary-list">
          <div>
            <dt>Solicitante</dt>
            <dd>{{ shipment.usuarioSolicitanteId }}</dd>
          </div>
          <div>
            <dt>Observaciones</dt>
            <dd>{{ shipment.observaciones || "—" }}</dd>
          </div>
        </dl></BaseCard
      >
    </div>
    <BaseCard
      v-else-if="tab === 'equipment'"
      title="Equipos del envío"
      :padded="false"
      ><BaseTable :columns="equipCols" :rows="mappedEquipment"
    /></BaseCard>
    <BaseCard v-else-if="tab === 'history'" title="Historial real del envío"
      ><ShipmentTimeline :events="events"
    /></BaseCard>
    <BaseCard v-else-if="tab === 'transport'" title="Transportación"
      ><div v-if="transport">
        <dl class="summary-list">
          <div>
            <dt>Tipo</dt>
            <dd>{{ transport.nombreTipo }}</dd>
          </div>
          <div>
            <dt>{{ isInternalTransport(transport.estrategia) ? "Chofer interno" : "Responsable privado" }}</dt>
            <dd>{{ transport.nombreChofer || transport.nombreResponsable || "—" }}</dd>
          </div>
          <div>
            <dt>Placa</dt>
            <dd>{{ transport.placaVehiculo || "—" }}</dd>
          </div>
          <div v-if="isInternalTransport(transport.estrategia)"><dt>Número de empleado</dt><dd>{{ transport.numeroEmpleado || "—" }}</dd></div>
          <div v-else><dt>Parentesco</dt><dd>{{ transport.parentesco }}</dd></div>
          <div v-if="isPrivateTransport(transport.estrategia)"><dt>Cédula</dt><dd>{{ transport.cedulaResponsable }}</dd></div>
        </dl>
        <button
          v-if="
            auth.can('transportes.confirmar') &&
            stateCode(shipment.estadoEnvioId) ===
              'PENDIENTE_CONFIRMACION_TRANSPORTE' &&
            !transport.entregaConfirmada
          "
          class="btn btn-primary"
          :disabled="confirmingTransport"
          @click="confirmTransport"
        >
          {{
            confirmingTransport ? "Confirmando…" : "Confirmar transportación"
          }}
        </button>
        <button v-if="stateCode(shipment.estadoEnvioId) === 'EN_TRANSITO' && ((isInternalTransport(transport.estrategia) && auth.can('transportes.confirmar')) || (isPrivateTransport(transport.estrategia) && auth.can('recepciones.gestionar')))" class="btn btn-primary" :disabled="registeringArrival" @click="registerTechnologyArrival">{{ registeringArrival ? "Registrando…" : (isInternalTransport(transport.estrategia)?"Confirmar llegada a Tecnología":"Confirmar recepción privada") }}</button>
      </div>
      <div v-else class="empty-state">
        No hay registro de transportación.
      </div></BaseCard
    >
    <BaseCard v-else-if="tab === 'reception'" title="Recepción"
      ><div v-if="reception">
        <dl class="summary-list">
          <div>
            <dt>Estado</dt>
            <dd>{{ reception.estadoRecepcion }}</dd>
          </div>
          <div>
            <dt>Fecha</dt>
            <dd>
              {{
                reception.fechaRecepcion
                  ? new Date(reception.fechaRecepcion).toLocaleString("es-DO")
                  : "Pendiente"
              }}
            </dd>
          </div>
          <div>
            <dt>Observaciones</dt>
            <dd>{{ reception.observaciones || "—" }}</dd>
          </div>
        </dl>
      </div>
      <div v-else class="empty-state">
        No hay registro de recepción.
      </div></BaseCard
    >
    <BaseCard v-else title="Incidencias"
      ><div v-if="incidents.length">
        <ul>
          <li v-for="item in incidents" :key="item.incidenciaId">
            {{ item.descripcion }}
          </li>
        </ul>
      </div>
      <div v-else class="empty-state">
        No hay incidencias registradas.
      </div></BaseCard
    >
  </div>
  <div v-else class="empty-state">No se encontró el envío.</div>
  <div
    v-if="deliveryOpen"
    class="confirm-overlay"
    role="dialog"
    aria-modal="true"
  >
    <div class="confirm-dialog">
      <h2>{{ isPrivateTransport(transport?.estrategia) ? "Entregar a transporte privado" : "Entregar a transportación" }}</h2>
      <p>
        {{ isPrivateTransport(transport?.estrategia)
          ? "Confirma la entrega al responsable privado. El envío pasará directamente a tránsito hacia Tecnología."
          : "Confirma la entrega física al chofer interno. El envío quedará pendiente de confirmación por Transportación." }}
      </p>
      <div class="confirm-actions">
        <button
          class="btn btn-secondary"
          type="button"
          @click="deliveryOpen = false"
        >
          Cancelar</button
        ><button
          class="btn btn-primary"
          type="button"
          :disabled="delivering"
          @click="deliverToTransport"
        >
          {{ delivering ? "Procesando…" : "Confirmar entrega" }}
        </button>
      </div>
    </div>
  </div>
</template>
