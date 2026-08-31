<script setup>
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ArrowLeft, Save, X } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import ShipmentSection from "@/components/shipments/ShipmentSection.vue";
import ShipmentGeneralSection from "@/components/shipments/ShipmentGeneralSection.vue";
import ShipmentTransportSection from "@/components/shipments/ShipmentTransportSection.vue";
import ShipmentEquipmentSection from "@/components/shipments/ShipmentEquipmentSection.vue";
import ShipmentSummary from "@/components/shipments/ShipmentSummary.vue";
import { catalogoService } from "@/services/catalogoService";
import { equipoService } from "@/services/equipoService";
import { envioService } from "@/services/envioService";
import { transporteService } from "@/services/transporteService";
import { isInternalTransport, isPrivateTransport } from "@/utils/transport";
import { useUiStore } from "@/stores/uiStore";
import { confirmAction, escapeHtml } from "@/utils/confirm";
import "@/assets/styles/shipment-create.css";

const router = useRouter(),
  route = useRoute(),
  ui = useUiStore(),
  loading = ref(true),
  saving = ref(false),
  registering = ref(false),
  confirmOpen = ref(false),
  locations = ref([]),
  types = ref([]),
  transportTypes = ref([]),
  drivers = ref([]),
  registeredEquipment = ref([]),
  allEquipment = ref([]),
  errors = reactive({
    origin: "",
    destination: "",
    transportType: "",
    internalDriver: "",
    privateName: "",
    relationship: "",
    privateId: "",
    vehiclePlate: "",
  });
const form = reactive({
  originId: "",
  destinationId: "",
  notes: "",
  transportTypeId: "",
  internalDriverId: "",
  privateName: "",
  relationship: "",
  privateId: "",
  vehiclePlate: "",
  equipment: [],
});
const item = reactive({
  existingId: "",
  typeId: "",
  brand: "",
  model: "",
  serial: "",
  assetCode: "",
  ticket: "",
  notes: "",
});
const itemErrors = reactive({
  typeId: "",
  brand: "",
  model: "",
  serial: "",
  ticket: "",
});
const editing = computed(() => Boolean(route.params.id));
const origin = computed(() =>
  locations.value.find((x) => x.ubicacionId === Number(form.originId)),
);
const destination = computed(() =>
  locations.value.find((x) => x.ubicacionId === Number(form.destinationId)),
);
const selectedTransportType = computed(() => transportTypes.value.find((x) => x.tipoTransporteId === Number(form.transportTypeId)));
const selectedDriver = computed(() => drivers.value.find((x) => x.choferInternoId === Number(form.internalDriverId)));
const isTechnology = (location) =>
  location?.tipo === 2 || String(location?.tipo).toLowerCase() === "tecnologia";
const destinationLocations = computed(() =>
  origin.value
    ? isTechnology(origin.value)
      ? locations.value.filter((x) => !isTechnology(x))
      : locations.value.filter(isTechnology)
    : locations.value,
);
const availableEquipment = computed(() =>
  form.originId
    ? registeredEquipment.value.filter(
        (x) => Number(x.ubicacionActualId) === Number(form.originId),
      )
    : registeredEquipment.value,
);
const flowLabel = computed(() =>
  !origin.value || !destination.value
    ? ""
    : isTechnology(origin.value)
      ? "Tecnología → Filial"
      : "Filial → Tecnología",
);
function resetItem() {
  Object.keys(item).forEach((k) => (item[k] = ""));
}
function fill(e) {
  item.existingId = e.equipoId;
  item.typeId = e.tipoEquipoId;
  item.brand = e.marca || "";
  item.model = e.modelo || "";
  item.serial = e.numeroSerie || "";
  item.assetCode = e.codigoActivo || "";
}
function lookup() {
  const s = item.serial?.trim().toLowerCase(),
    a = item.assetCode?.trim().toLowerCase(),
    m = registeredEquipment.value.find(
      (e) =>
        (s && e.numeroSerie?.toLowerCase() === s) ||
        (a && e.codigoActivo?.toLowerCase() === a),
    );
  if (m) fill(m);
}
function ensure() {
  if (item.existingId) return Promise.resolve(Number(item.existingId));
  const d = registeredEquipment.value.find(
    (e) =>
      e.numeroSerie?.toLowerCase() === item.serial.toLowerCase() ||
      e.codigoActivo?.toLowerCase() === item.assetCode?.toLowerCase(),
  );
  if (d) {
    fill(d);
    return Promise.resolve(d.equipoId);
  }
  registering.value = true;
  return equipoService
    .create({
      tipoEquipoId: Number(item.typeId),
      ubicacionActualId: Number(form.originId),
      codigoActivo: item.assetCode || null,
      numeroSerie: item.serial,
      marca: item.brand,
      modelo: item.model,
      observaciones: item.notes || null,
    })
    .then((r) => {
      registeredEquipment.value.push({
        equipoId: r.data,
        tipoEquipoId: Number(item.typeId),
        codigoActivo: item.assetCode,
        numeroSerie: item.serial,
        marca: item.brand,
        modelo: item.model,
      });
      return r.data;
    })
    .finally(() => (registering.value = false));
}
async function addEquipment() {
  itemErrors.typeId = item.typeId ? "" : "Tipo de equipo";
  itemErrors.brand = item.brand ? "" : "Marca";
  itemErrors.model = item.model ? "" : "Modelo";
  itemErrors.serial = item.serial ? "" : "Serial";
  itemErrors.ticket = !item.ticket
    ? "Ticket"
    : !/^\d+$/.test(item.ticket)
      ? "El ticket solo puede contener números"
      : "";
  if (!form.originId) {
    ui.notify("Selecciona el origen antes de agregar un equipo.", "warning");
    return;
  }
  const duplicate =
    (item.existingId &&
      form.equipment.some(
        (e) => Number(e.existingId) === Number(item.existingId),
      )) ||
    (item.serial &&
      form.equipment.some(
        (e) => e.serial?.toLowerCase() === item.serial.toLowerCase(),
      )) ||
    form.equipment.some((e) => e.ticket === item.ticket) ||
    (item.assetCode &&
      form.equipment.some(
        (e) => e.assetCode?.toLowerCase() === item.assetCode.toLowerCase(),
      ));
  if (duplicate) {
    ui.notify("Este equipo ya fue agregado a este envío.", "warning");
    return;
  }
  if (Object.values(itemErrors).some(Boolean)) return;
  try {
    const id = await ensure();
    form.equipment.push({
      id: Date.now(),
      existingId: id,
      typeId: item.typeId,
      brand: item.brand,
      model: item.model,
      serial: item.serial,
      assetCode: item.assetCode,
      ticket: item.ticket,
      notes: item.notes,
      typeName:
        types.value.find((x) => x.tipoEquipoId === Number(item.typeId))
          ?.nombre || `Tipo #${item.typeId}`,
    });
    ui.notify("Equipo listo para asociar al envío.", "success");
    resetItem();
    Object.keys(itemErrors).forEach((k) => (itemErrors[k] = ""));
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible registrar el equipo.", "error");
  }
}
function removeEquipment(id) {
  form.equipment = form.equipment.filter((x) => x.id !== id);
}
function editEquipment(e) {
  Object.assign(item, e);
  removeEquipment(e.id);
}
async function load() {
  try {
    const [l, t, e, tt, d] = await Promise.all([
      catalogoService.locations(),
      catalogoService.types(),
      equipoService.list(),
      catalogoService.transportTypes(),
      catalogoService.internalDrivers(),
    ]);
    locations.value = l.data || [];
    types.value = t.data || [];
    registeredEquipment.value = e.data || [];
    transportTypes.value = tt.data || [];
    drivers.value = d.data || [];
    if (editing.value) {
      const r = await envioService.get(route.params.id);
      form.originId = r.data.ubicacionOrigenId;
      form.destinationId = r.data.ubicacionDestinoId;
      form.notes = r.data.observaciones || "";
    }
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cargar los catálogos.", "error");
  } finally {
    loading.value = false;
  }
}
function validate() {
  errors.origin = !form.originId ? "Selecciona el origen." : "";
  errors.destination = !form.destinationId
    ? "Selecciona el destino."
    : form.originId === form.destinationId
      ? "El destino debe ser diferente al origen."
      : "";
  const selected = transportTypes.value.find(
    (x) => x.tipoTransporteId === Number(form.transportTypeId),
  );
  if (!editing.value) {
    errors.transportType = !selected
      ? "Selecciona el tipo de transporte."
      : isPrivateTransport(selected.estrategia) && isTechnology(origin.value)
        ? "El transporte privado directo solo aplica a envíos hacia Tecnología."
        : "";
    errors.internalDriver =
      isInternalTransport(selected?.estrategia) && !form.internalDriverId
        ? "Selecciona el chofer."
        : "";
    errors.privateName =
      isPrivateTransport(selected?.estrategia) && !form.privateName
        ? "Indica el nombre."
        : "";
    errors.relationship =
      isPrivateTransport(selected?.estrategia) && !form.relationship
        ? "Indica el parentesco."
        : "";
    errors.privateId =
      isPrivateTransport(selected?.estrategia) && !/^\d{11}$/.test(form.privateId)
        ? "La cédula debe tener 11 dígitos."
        : "";
    errors.vehiclePlate =
      isPrivateTransport(selected?.estrategia) && !form.vehiclePlate
        ? "Indica la placa."
        : "";
  }
  if (!editing.value && !form.equipment.length)
    ui.notify("Agrega al menos un equipo antes de guardar.", "warning");
  return (
    !Object.values(errors).some(Boolean) &&
    (editing.value || form.equipment.length)
  );
}
async function submit() {
  if (!validate()) return;
  if (!editing.value) {
    const availability = await Promise.all(form.equipment.map((equipment) => envioService.ticketAvailable(equipment.ticket)));
    const duplicateIndex = availability.findIndex((response) => response.data !== true);
    if (duplicateIndex >= 0) {
      ui.notify(`El ticket ${form.equipment[duplicateIndex].ticket} ya está asociado a otro equipo o envío.`, "error");
      return;
    }
  }
  const transportLabel = selectedTransportType.value?.nombre || "Sin indicar";
  const driverLabel = isInternalTransport(selectedTransportType.value?.estrategia)
    ? selectedDriver.value?.nombreCompleto || "Sin indicar"
    : `${form.privateName || "Sin indicar"} · ${form.vehiclePlate || "Sin placa"}`;
  const equipmentHtml = form.equipment.length
    ? form.equipment.map((equipment, index) => `<div style="border:1px solid #e1e6ee;border-radius:6px;padding:9px;margin:7px 0"><strong>${index + 1}. ${escapeHtml(equipment.typeName)}</strong><br><small>${escapeHtml(equipment.brand)} ${escapeHtml(equipment.model)} · Serial: ${escapeHtml(equipment.serial)} · Activo: ${escapeHtml(equipment.assetCode)} · Ticket: ${escapeHtml(equipment.ticket)}</small></div>`).join("")
    : "<p>Los equipos asociados se conservarán sin cambios.</p>";
  const accepted = await confirmAction({
    title: editing.value ? "Confirmar actualización" : "Confirmar nuevo envío",
    html: `<div style="text-align:left"><p><strong>Origen:</strong> ${escapeHtml(origin.value?.nombre)}</p><p><strong>Destino:</strong> ${escapeHtml(destination.value?.nombre)}</p><p><strong>Transporte:</strong> ${escapeHtml(transportLabel)}</p>${editing.value ? "" : `<p><strong>Responsable/chofer:</strong> ${escapeHtml(driverLabel)}</p>`}<p><strong>Observaciones:</strong> ${escapeHtml(form.notes || "Sin observaciones")}</p><hr><strong>Equipos (${form.equipment.length})</strong><div style="max-height:280px;overflow:auto">${equipmentHtml}</div></div>`,
    confirmText: editing.value ? "Guardar cambios" : "Crear envío",
  });
  if (accepted) await save();
}
async function save() {
  confirmOpen.value = false;
  saving.value = true;
  try {
    const r = editing.value
      ? await envioService.update(route.params.id, {
          ubicacionOrigenId: Number(form.originId),
          ubicacionDestinoId: Number(form.destinationId),
          observaciones: form.notes || null,
        })
      : await envioService.create({
          ubicacionOrigenId: Number(form.originId),
          ubicacionDestinoId: Number(form.destinationId),
          observaciones: form.notes || null,
        });
    const id = editing.value ? route.params.id : r.data.envioId;
    if (!editing.value) {
      for (const e of form.equipment)
        await envioService.addEquipment({
          envioId: Number(id),
          equipoId: Number(e.existingId),
          numeroTicket: e.ticket,
          observaciones: e.notes || "Equipo asociado al envío.",
        });
      await transporteService.create({
        envioId: Number(id),
        tipoTransporteId: Number(form.transportTypeId),
        choferInternoId: form.internalDriverId
          ? Number(form.internalDriverId)
          : null,
        nombreResponsable: form.privateName || null,
        parentesco: form.relationship || null,
        cedulaResponsable: form.privateId || null,
        placaVehiculo: form.vehiclePlate || null,
      });
    }
    ui.notify(
      editing.value
        ? "Envío actualizado correctamente."
        : "Envío creado correctamente.",
    );
    router.push(`/envios/${id}`);
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible guardar el envío.", "error");
  } finally {
    saving.value = false;
  }
}
watch(
  () => form.originId,
  () => {
    if (
      form.destinationId &&
      !destinationLocations.value.some(
        (x) => x.ubicacionId === Number(form.destinationId),
      )
    )
      form.destinationId = "";
    if (!editing.value && origin.value && !isTechnology(origin.value)) {
      const technologyHeadquarters = locations.value.find((location) =>
        isTechnology(location) &&
        (/centro\s*sede/i.test(location.nombre || "") || /tecnolog/i.test(location.nombre || "")),
      );
      if (technologyHeadquarters) form.destinationId = technologyHeadquarters.ubicacionId;
    }
  },
);
watch(() => [item.serial, item.assetCode], lookup);
watch(
  () => item.existingId,
  (id) => {
    const e = registeredEquipment.value.find((x) => x.equipoId === Number(id));
    if (e) fill(e);
  },
);
onMounted(load);
</script>
<template>
  <div class="shipment-create-page">
    <PageHeader
      :title="editing ? 'Editar envío' : 'Crear envío'"
      subtitle="Registra la información logística y los equipos asociados"
      ><button
        class="btn btn-secondary"
        type="button"
        @click="router.push('/envios')"
      >
        <ArrowLeft :size="16" /> Cancelar</button
      ><button
        class="btn btn-primary"
        type="button"
        :disabled="saving || loading"
        @click="submit"
      >
        <Save :size="16" /> {{ saving ? "Guardando…" : "Guardar envío" }}
      </button></PageHeader
    >
    <div v-if="loading" class="loading-state">Cargando catálogos…</div>
    <form v-else class="shipment-create-layout" @submit.prevent="submit">
      <main class="shipment-create-main">
        <ShipmentSection title="Información general"
          ><ShipmentGeneralSection
            :form="form"
            :errors="errors"
            :locations="locations"
            :destination-locations="destinationLocations"
            :flow-label="flowLabel" /></ShipmentSection
        ><ShipmentSection v-if="!editing" title="Transportación"
          ><ShipmentTransportSection
            :form="form"
            :transport-types="transportTypes"
            :drivers="drivers"
            :errors="errors" /></ShipmentSection
        ><ShipmentSection v-if="!editing" title="Equipos"
          ><ShipmentEquipmentSection
            :item="item"
            :equipments="form.equipment"
            :types="types"
            :registered-equipment="registeredEquipment"
            :registering="registering"
            @add="addEquipment"
            @remove="removeEquipment"
            @edit="editEquipment"
            @new="resetItem"
        /></ShipmentSection>
      </main>
      <ShipmentSummary
        :form="form"
        :origin-name="origin?.nombre"
        :destination-name="destination?.nombre"
        :flow-label="flowLabel"
      />
    </form>
    <div
      v-if="confirmOpen"
      class="confirm-overlay"
      role="dialog"
      aria-modal="true"
    >
      <div class="confirm-dialog">
        <button
          class="confirm-close"
          type="button"
          aria-label="Cerrar"
          @click="confirmOpen = false"
        >
          <X :size="17" />
        </button>
        <h2>Confirmar guardado</h2>
        <p>¿Deseas guardar este envío con la información indicada?</p>
        <dl class="confirm-summary">
          <div>
            <dt>Origen</dt>
            <dd>{{ origin?.nombre || "Sin seleccionar" }}</dd>
          </div>
          <div>
            <dt>Destino</dt>
            <dd>{{ destination?.nombre || "Sin seleccionar" }}</dd>
          </div>
          <div>
            <dt>Flujo operativo</dt>
            <dd>{{ flowLabel || "Por definir" }}</dd>
          </div>
          <div>
            <dt>Tipo de transporte</dt>
            <dd>{{ selectedTransportType?.nombre || "No indicado" }}</dd>
          </div>
          <div>
            <dt>Chofer / responsable</dt>
            <dd>{{ selectedDriver?.nombreCompleto || form.privateName || "No indicado" }}</dd>
          </div>
          <div>
            <dt>Equipos</dt>
            <dd>{{ form.equipment.length }}</dd>
          </div>
          <div class="confirm-observations">
            <dt>Observaciones</dt>
            <dd>{{ form.notes || "Sin observaciones" }}</dd>
          </div>
        </dl>
        <div v-if="form.equipment.length" class="confirm-equipment-list">
          <h3>Equipos a asociar</h3>
          <article
            v-for="(equipment, index) in form.equipment"
            :key="equipment.id"
          >
            <strong>{{ index + 1 }}. {{ equipment.typeName }}</strong
            ><span
              >Marca / modelo: {{ equipment.brand }} {{ equipment.model }}</span
            ><span
              >Serial: {{ equipment.serial || "No indicado" }} · Código:
              {{ equipment.assetCode || "No indicado" }}</span
            ><span>Ticket: {{ equipment.ticket }}</span
            ><span v-if="equipment.notes"
              >Observación: {{ equipment.notes }}</span
            >
          </article>
        </div>
        <div class="confirm-actions">
          <button
            class="btn btn-secondary"
            type="button"
            @click="confirmOpen = false"
          >
            Cancelar</button
          ><button
            class="btn btn-primary"
            type="button"
            :disabled="saving"
            @click="save"
          >
            <Save :size="15" /> Confirmar y guardar
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
