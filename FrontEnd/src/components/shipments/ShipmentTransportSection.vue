<script setup>
import { computed, watch } from "vue";
import { isInternalTransport } from "@/utils/transport";
import {
  TIPOS_DOCUMENTO,
  TIPO_CEDULA,
  etiquetaDocumento,
  filtrarDocumento,
  largoMaximo,
  modoEntrada,
} from "@/utils/documento";
const props = defineProps({
  form: Object,
  transportTypes: { type: Array, default: () => [] },
  drivers: { type: Array, default: () => [] },
  errors: { type: Object, default: () => ({}) },
});
const selected = computed(() =>
  props.transportTypes.find((x) => x.tipoTransporteId === Number(props.form.transportTypeId)),
);
const internal = computed(() => isInternalTransport(selected.value?.estrategia));

// El tipo decide qué se puede teclear: la cédula no admite letras, el pasaporte sí.
const tipoDocumento = computed(() => Number(props.form.documentType) || TIPO_CEDULA);
const etiqueta = computed(() => etiquetaDocumento(tipoDocumento.value));

// Se filtra al escribir y no solo al enviar: dejar teclear una letra en la cédula para después
// rechazarla es hacerle perder el tiempo a quien la escribe.
function alEscribirDocumento(evento) {
  const limpio = filtrarDocumento(tipoDocumento.value, evento.target.value);
  props.form.privateId = limpio;
  evento.target.value = limpio;
}

// Cambiar de tipo limpia el campo: un número de cédula no es un pasaporte válido y viceversa,
// así que conservarlo solo produce un error que el usuario no sabe de dónde sale.
watch(tipoDocumento, () => {
  props.form.privateId = "";
});

watch(
  () => props.form.transportTypeId,
  () => {
    if (internal.value)
      Object.assign(props.form, {
        privateName: "",
        relationship: "",
        documentType: TIPO_CEDULA,
        privateId: "",
        vehiclePlate: "",
      });
    else props.form.internalDriverId = "";
  },
);
</script>
<template>
  <div class="form-grid">
    <label
      >Tipo de transporte *<select v-model="form.transportTypeId" class="form-control">
        <option value="">Seleccionar</option>
        <option
          v-for="type in transportTypes"
          :key="type.tipoTransporteId"
          :value="type.tipoTransporteId"
        >
          {{ type.nombre }}
        </option></select
      ><small v-if="errors.transportType" class="field-error">{{
        errors.transportType
      }}</small></label
    >
    <template v-if="selected && internal"
      ><label
        >Chofer interno *<select v-model="form.internalDriverId" class="form-control">
          <option value="">Seleccionar chofer</option>
          <option
            v-for="driver in drivers"
            :key="driver.choferInternoId"
            :value="driver.choferInternoId"
          >
            {{ driver.nombreCompleto }} · {{ driver.numeroEmpleado }}
          </option></select
        ><small v-if="errors.internalDriver" class="field-error">{{
          errors.internalDriver
        }}</small></label
      ></template
    >
    <template v-else-if="selected"
      ><label
        >Nombre del responsable *<input
          v-model.trim="form.privateName"
          class="form-control"
        /><small v-if="errors.privateName" class="field-error">{{
          errors.privateName
        }}</small></label
      ><label
        >Parentesco *<select v-model="form.relationship" class="form-control">
          <option value="">Seleccionar</option>
          <option>Padre</option>
          <option>Madre</option>
          <option>Hermano/a</option>
          <option>Esposo/a</option>
          <option>Hijo/a</option>
          <option>Tutor</option>
          <option>Representante autorizado</option>
          <option>Otro</option></select
        ><small v-if="errors.relationship" class="field-error">{{
          errors.relationship
        }}</small></label
      ><label
        >Tipo de documento *<select v-model="form.documentType" class="form-control">
          <option v-for="tipo in TIPOS_DOCUMENTO" :key="tipo.valor" :value="tipo.valor">
            {{ tipo.etiqueta }}
          </option>
        </select></label
      ><label
        >{{ etiqueta }} *<input
          :value="form.privateId"
          :maxlength="largoMaximo(tipoDocumento)"
          :inputmode="modoEntrada(tipoDocumento)"
          class="form-control"
          @input="alEscribirDocumento"
        /><small v-if="errors.privateId" class="field-error">{{ errors.privateId }}</small></label
      ><label
        >Placa del vehículo *<input v-model.trim="form.vehiclePlate" class="form-control" /><small
          v-if="errors.vehiclePlate"
          class="field-error"
          >{{ errors.vehiclePlate }}</small
        ></label
      ></template
    >
  </div>
</template>
