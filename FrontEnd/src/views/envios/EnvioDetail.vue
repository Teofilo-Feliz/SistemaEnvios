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
import EquipoInfoCard from "@/components/equipos/EquipoInfoCard.vue";
import ModalCard from "@/components/common/ModalCard.vue";
import { envioService } from "@/services/envioService";
import { equipoService } from "@/services/equipoService";
import { transporteService } from "@/services/transporteService";
import { catalogoService } from "@/services/catalogoService";
import { filas } from '@/services/paginacion';
import { useUiStore } from "@/stores/uiStore";
import { useAuthStore } from "@/stores/authStore";
import { isInternalTransport, isPrivateTransport } from "@/utils/transport";
import { confirmAction, notifyNotificationsChanged } from "@/utils/confirm";
import { useRefrescoAlVolver } from "@/composables/useRefrescoAlVolver";

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
const equipoDetails = ref({});
const equipoTypes = ref([]);
const selectedEquipoId = ref(null);
const history = ref([]);
const transport = ref(null);
const reception = ref(null);
const incidents = ref([]);
// Equipos marcados con incidencia al recibirlos, que es lo que explica el estado en rojo.
const incidenciasRecepcion = ref([]);
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
  { key: "serial", label: "Serial" },
  { key: "asset", label: "Código activo" },
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
const esEnvioHaciaFilial = computed(() => {
  const direccion = shipment.value?.direccion;
  return direccion === 2 || String(direccion).toLowerCase() === "haciafilial" || String(direccion).toLowerCase().includes("filial");
});
const flowLabel = computed(() =>
  esEnvioHaciaFilial.value
    ? "Tecnología → Filial"
    : "Filial → Tecnología",
);
// Editable mientras no haya empezado a moverse. Cada dirección tiene su estado de partida:
// la filial prepara en EN_FILIAL y Tecnología en PREPARACION_TECNOLOGIA.
const esEditable = computed(() =>
  ["EN_FILIAL", "PREPARACION_TECNOLOGIA"].includes(stateCode(shipment.value?.estadoEnvioId)),
);
const ESTADOS_CON_INCIDENCIA = ["RECIBIDO_FILIAL_INCIDENCIA", "RECIBIDO_TECNOLOGIA_INCIDENCIA"];

const events = computed(() =>
  history.value.map((x) => ({
    label: stateName(x.estadoEnvioId),
    date: new Date(x.fecha).toLocaleString("es-DO"),
    // Antes se mostraba el id del usuario, que sin sincronización con AuthManager no se
    // resuelve a un nombre. La observación del movimiento sí explica qué pasó.
    nota: x.observaciones || "",
    incidencia: ESTADOS_CON_INCIDENCIA.includes(stateCode(x.estadoEnvioId)),
    current: x.estadoEnvioId === shipment.value?.estadoEnvioId,
  })),
);

// El botón del recorrido abre las incidencias sin sacar al usuario de donde está mirando.
const incidenciasAbiertas = ref(false);
// La asociación envío-equipo solo trae el equipoId; la marca, el modelo y el serial salen
// del equipo, que se carga aparte (equipoDetails) para no mostrar "Equipo #12".
const mappedEquipment = computed(() =>
  equipment.value.map((x) => {
    const detalle = equipoDetails.value[x.equipoId];
    return {
      id: x.equipoId,
      equipment: detalle ? `${detalle.marca} ${detalle.modelo}` : `Equipo #${x.equipoId}`,
      serial: detalle?.numeroSerie || "—",
      asset: detalle?.codigoActivo || "—",
      ticket: x.numeroTicket || "—",
      observations: x.observaciones || "—",
    };
  }),
);
const equipoSeleccionado = computed(() =>
  selectedEquipoId.value ? equipoDetails.value[selectedEquipoId.value] || null : null,
);
const tipoDeEquipo = (equipo) =>
  equipoTypes.value.find((x) => x.tipoEquipoId === equipo.tipoEquipoId)?.nombre || "Equipo";
// Si el detalle no quedó cacheado (esa consulta falló al abrir la pestaña), se pide ahora:
// sin esto el ojito no abriría nada y el usuario no sabría por qué.
async function verEquipo(row) {
  if (!equipoDetails.value[row.id]) {
    try {
      const { data } = await equipoService.get(row.id);
      equipoDetails.value = { ...equipoDetails.value, [data.equipoId]: data };
    } catch (error) {
      ui.showToast(
        error.userMessage || "No fue posible cargar la información del equipo.",
        "error",
      );
      return;
    }
  }
  selectedEquipoId.value = row.id;
}
// Cada equipo se pide por separado: no hay endpoint que devuelva el detalle de varios a la
// vez. Un fallo puntual no rompe la pestaña, solo deja esa fila con el identificador.
async function cargarEquipos() {
  selectedEquipoId.value = null;
  const ids = [...new Set(equipment.value.map((x) => x.equipoId))];
  if (!ids.length) {
    equipoDetails.value = {};
    return;
  }
  const [detalles, tipos] = await Promise.all([
    Promise.all(ids.map((id) => equipoService.get(id).catch(() => null))),
    catalogoService.allTypes().catch(() => []),
  ]);
  equipoDetails.value = Object.fromEntries(
    detalles.filter(Boolean).map((respuesta) => [respuesta.data.equipoId, respuesta.data]),
  );
  equipoTypes.value = tipos;
}

async function load() {
  loading.value = true;
  try {
    const requests = [
      envioService.get(route.params.id),
      catalogoService.locations({ pageSize: 100 }),
      catalogoService.states({ pageSize: 100 }),
      envioService.equipment(route.params.id, { pageSize: 100 }),
      envioService.history(route.params.id, { pageSize: 100 }),
      transporteService.getByEnvio(route.params.id),
      envioService.reception(route.params.id),
      envioService.incidents(route.params.id, { pageSize: 100 }),
      envioService.receptionIncidents(route.params.id, { pageSize: 100 }),
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
      recepcionIncidentsResult,
    ] = await Promise.all(
      requests.map((request) => request.catch(() => ({ data: null }))),
    );
    shipment.value = detail.data;
    locations.value = filas(locationResult);
    states.value = filas(stateResult);
    equipment.value = filas(equipmentResult);
    await cargarEquipos();
    history.value = filas(historyResult);
    transport.value = transportResult.data;
    reception.value = receptionResult.data;
    incidents.value = filas(incidentsResult);
    incidenciasRecepcion.value = filas(recepcionIncidentsResult);
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
      "ENTREGADO_TRANSPORTACION" ||
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
async function deliverTechnologyToTransport() {
  if (!(await confirmAction({ title: "Entregar a transportación", text: `¿Confirmas que el envío ${shipment.value.numeroEnvio} fue entregado a Transportación? Quedará esperando que le asignen chofer.`, confirmText: "Entregado a transportación" }))) return;
  delivering.value = true;
  try { await envioService.dispatchFromTechnology(route.params.id); ui.showToast("Entrega registrada. Transportación debe asignarle chofer.", "success"); await load(); }
  catch (error) { ui.showToast(error.userMessage || "No fue posible registrar la entrega.", "error"); }
  finally { delivering.value = false; }
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
// Al volver a esta pestaña los datos pueden haber cambiado en otra máquina.
useRefrescoAlVolver(load);
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
  <button v-if="!loading && shipment && esEnvioHaciaFilial && auth.can('envios.despachar') && stateCode(shipment.estadoEnvioId) === 'PREPARACION_TECNOLOGIA'" class="btn btn-primary shipment-delivery-action" :disabled="delivering" type="button" @click="deliverTechnologyToTransport">
    {{ delivering ? "Procesando…" : "Entregado a transportación" }}
  </button>
  <div v-if="loading" class="page-loading">Cargando envío…</div>
  <div v-else-if="shipment">
    <PageHeader
      :title="`Envío ${shipment.numeroEnvio}`"
      subtitle="Seguimiento integral y trazabilidad del envío"
      ><button class="btn btn-secondary" @click="router.back()">
        <ArrowLeft :size="16" /> Volver</button
      ><button v-if="esEditable && auth.can('envios.editar')" class="btn btn-primary" @click="router.push(`/envios/${shipment.envioId}/editar`)">
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
        ><ShipmentTimeline :events="events" @ver-incidencia="incidenciasAbiertas = true" /></BaseCard
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
      ><BaseTable :columns="equipCols" :rows="mappedEquipment" @view="verEquipo"
    /></BaseCard>
    <BaseCard v-else-if="tab === 'history'" title="Historial real del envío"
      ><ShipmentTimeline :events="events" @ver-incidencia="incidenciasAbiertas = true"
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
              'ENTREGADO_TRANSPORTACION' &&
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
      ><div v-if="incidenciasRecepcion.length" class="incidencias-recepcion">
        <h3>Equipos que llegaron con incidencia</h3>
        <article v-for="item in incidenciasRecepcion" :key="item.envioEquipoId">
          <header>
            <strong>{{ item.marca }} {{ item.modelo }}</strong>
            <span class="etiqueta-ticket">Ticket {{ item.numeroTicket }}</span>
          </header>
          <p class="identificacion">
            Serial: {{ item.numeroSerie || "—" }} · Activo: {{ item.codigoActivo || "—" }}
          </p>
          <p class="anotacion">{{ item.observaciones || "Se marcó con incidencia sin detallar." }}</p>
          <small v-if="item.fechaVerificacion">
            Verificado el {{ new Date(item.fechaVerificacion).toLocaleString("es-DO") }}
          </small>
        </article>
      </div>
      <div v-if="incidents.length" class="incidencias-registradas">
        <h3>Incidencias registradas aparte</h3>
        <ul>
          <li v-for="item in incidents" :key="item.incidenciaId">
            {{ item.descripcion }}
          </li>
        </ul>
      </div>
      <div v-if="!incidents.length && !incidenciasRecepcion.length" class="empty-state">
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

  <!-- Fuera del overlay de entrega: si queda dentro, solo se renderiza cuando ese
       diálogo está abierto y el ojito no muestra nada. -->
  <ModalCard
    :open="Boolean(equipoSeleccionado)"
    :eyebrow="equipoSeleccionado ? tipoDeEquipo(equipoSeleccionado) : ''"
    :title="equipoSeleccionado ? `${equipoSeleccionado.marca} ${equipoSeleccionado.modelo}` : ''"
    @close="selectedEquipoId = null"
  >
    <EquipoInfoCard
      v-if="equipoSeleccionado"
      :equipo="equipoSeleccionado"
      :types="equipoTypes"
      :locations="locations"
    />
    <template #footer>
      <button
        v-if="equipoSeleccionado"
        class="btn btn-ghost"
        type="button"
        @click="router.push(`/equipos/${equipoSeleccionado.equipoId}`)"
      >
        Abrir ficha completa
      </button>
    </template>
  </ModalCard>

  <ModalCard
    :open="incidenciasAbiertas"
    eyebrow="Recepción con incidencia"
    :title="shipment ? `Envío ${shipment.numeroEnvio}` : ''"
    @close="incidenciasAbiertas = false"
  >
    <div v-if="incidenciasRecepcion.length" class="incidencias-recepcion">
      <article v-for="item in incidenciasRecepcion" :key="item.envioEquipoId">
        <header>
          <strong>{{ item.marca }} {{ item.modelo }}</strong>
          <span class="etiqueta-ticket">Ticket {{ item.numeroTicket }}</span>
        </header>
        <p class="identificacion">
          Serial: {{ item.numeroSerie || "—" }} · Activo: {{ item.codigoActivo || "—" }}
        </p>
        <p class="anotacion">{{ item.observaciones || "Se marcó con incidencia sin detallar." }}</p>
        <small v-if="item.fechaVerificacion">
          Verificado el {{ new Date(item.fechaVerificacion).toLocaleString("es-DO") }}
        </small>
      </article>
    </div>
    <div v-else class="empty-state">
      El envío quedó marcado con incidencia pero no hay equipos anotados.
    </div>
  </ModalCard>
</template>

<style scoped>
.modal-link {
  font-size: 12.5px;
}
</style>
