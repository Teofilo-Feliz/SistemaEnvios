<script setup>
import { computed, onMounted, reactive, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { ArrowLeft, Save } from "lucide-vue-next";
import PageHeader from "@/components/common/PageHeader.vue";
import ShipmentSection from "@/components/shipments/ShipmentSection.vue";
import ShipmentTransportSection from "@/components/shipments/ShipmentTransportSection.vue";
import { envioService } from "@/services/envioService";
import { filas } from "@/services/paginacion";
import { transporteService } from "@/services/transporteService";
import { catalogoService } from "@/services/catalogoService";
import { useUiStore } from "@/stores/uiStore";
import { isInternalTransport, isPrivateTransport } from "@/utils/transport";
import { TIPO_CEDULA, errorDocumento } from "@/utils/documento";
import { confirmAction } from "@/utils/confirm";
import "@/assets/styles/shipment-create.css";
const router = useRouter(),
  ui = useUiStore(),
  loading = ref(true),
  saving = ref(false),
  shipments = ref([]),
  transportTypes = ref([]),
  drivers = ref([]),
  shipmentStates = ref({});
const selectedShipment = computed(() =>
  shipments.value.find((x) => x.envioId === Number(form.shipmentId)),
);
// Desde esta bandeja Transportación siempre asigna un vehículo institucional.
const availableTransportTypes = computed(() =>
  transportTypes.value.filter((x) => isInternalTransport(x.estrategia)),
);
const errors = reactive({
  shipment: "",
  transportType: "",
  internalDriver: "",
  privateName: "",
  relationship: "",
  documentType: TIPO_CEDULA,
  privateId: "",
  vehiclePlate: "",
});
const form = reactive({
  shipmentId: "",
  transportTypeId: "",
  internalDriverId: "",
  privateName: "",
  relationship: "",
  privateId: "",
  vehiclePlate: "",
  notes: "",
});
async function load() {
  try {
    // La etapa se pide al servidor; aquí solo queda descartar los que ya tienen transporte.
    const [e, t, d, states] = await Promise.all([
      envioService.paged({ pageSize: 100, estadoCodigos: ["EN_TRANSPORTACION"] }),
      catalogoService.allTransportTypes(),
      catalogoService.allInternalDrivers(),
      catalogoService.allStates(),
    ]);
    shipmentStates.value = Object.fromEntries(states.map((x) => [x.estadoEnvioId, x.codigo]));
    shipments.value = filas(e).filter((x) => !x.tipoTransporteId);
    transportTypes.value = t;
    drivers.value = d;
    const internal = transportTypes.value.find((x) => isInternalTransport(x.estrategia));
    if (internal) form.transportTypeId = internal.tipoTransporteId;
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible cargar los catálogos.", "error");
  } finally {
    loading.value = false;
  }
}
watch(
  () => form.shipmentId,
  () => {
    if (
      selectedShipment.value &&
      shipmentStates.value[selectedShipment.value.estadoEnvioId] === "EN_TRANSPORTACION"
    ) {
      const internal = transportTypes.value.find((x) => isInternalTransport(x.estrategia));
      if (internal) form.transportTypeId = internal.tipoTransporteId;
      form.privateName = form.relationship = form.privateId = form.vehiclePlate = "";
      form.documentType = TIPO_CEDULA;
    }
  },
);
async function save() {
  const selected = transportTypes.value.find(
    (x) => x.tipoTransporteId === Number(form.transportTypeId),
  );
  errors.shipment = form.shipmentId ? "" : "Selecciona el envío.";
  errors.transportType = selected ? "" : "Selecciona el tipo.";
  errors.internalDriver =
    isInternalTransport(selected?.estrategia) && !form.internalDriverId
      ? "Selecciona el chofer."
      : "";
  errors.privateName =
    isPrivateTransport(selected?.estrategia) && !form.privateName ? "Indica el nombre." : "";
  errors.relationship =
    isPrivateTransport(selected?.estrategia) && !form.relationship ? "Indica el parentesco." : "";
  // El formato depende del tipo de documento; la regla vive en utils/documento.js.
  errors.privateId = isPrivateTransport(selected?.estrategia)
    ? errorDocumento(form.documentType, form.privateId)
    : "";
  errors.vehiclePlate =
    isPrivateTransport(selected?.estrategia) && !form.vehiclePlate ? "Indica la placa." : "";
  if (Object.values(errors).some(Boolean)) return;
  const accepted = await confirmAction({
    title: "Confirmar transporte",
    text: "¿Deseas guardar la asignación de transporte para este envío?",
    confirmText: "Guardar transporte",
  });
  if (!accepted) return;
  saving.value = true;
  try {
    await transporteService.create({
      envioId: Number(form.shipmentId),
      tipoTransporteId: Number(form.transportTypeId),
      choferInternoId: form.internalDriverId ? Number(form.internalDriverId) : null,
      nombreResponsable: form.privateName || null,
      parentesco: form.relationship || null,
      tipoDocumento: Number(form.documentType) || TIPO_CEDULA,
      documentoResponsable: form.privateId || null,
      placaVehiculo: form.vehiclePlate || null,
      observaciones: form.notes || null,
    });
    ui.notify("Transporte registrado correctamente.");
    router.push(`/envios/${form.shipmentId}`);
  } catch (e) {
    ui.notify(e.userMessage || "No fue posible registrar el transporte.", "error");
  } finally {
    saving.value = false;
  }
}
onMounted(load);
</script>
<template>
  <div class="shipment-create-page">
    <PageHeader
      title="Asignar transporte"
      subtitle="Asigna el chofer al envío entregado por Tecnología"
      ><button class="btn btn-secondary" @click="router.push('/transportacion')">
        <ArrowLeft :size="16" /> Cancelar</button
      ><button class="btn btn-primary" :disabled="saving || loading" @click="save">
        <Save :size="16" /> Guardar registro
      </button></PageHeader
    >
    <div v-if="loading" class="loading-state">Cargando…</div>
    <form v-else class="equipment-create-layout" @submit.prevent="save">
      <main class="form-main">
        <ShipmentSection title="Envío"
          ><label
            >Envío *<select v-model="form.shipmentId" class="form-control">
              <option value="">Seleccionar</option>
              <option v-for="x in shipments" :key="x.envioId" :value="x.envioId">
                {{ x.numeroEnvio }}
              </option></select
            ><small v-if="errors.shipment" class="field-error">{{ errors.shipment }}</small></label
          ></ShipmentSection
        ><ShipmentSection title="Datos de transporte"
          ><ShipmentTransportSection
            :form="form"
            :transport-types="availableTransportTypes"
            :drivers="drivers"
            :errors="errors"
        /></ShipmentSection>
      </main>
    </form>
  </div>
</template>
