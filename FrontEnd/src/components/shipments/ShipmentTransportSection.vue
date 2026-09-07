<script setup>
import { computed, watch } from "vue";
import { isInternalTransport } from "@/utils/transport";
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
watch(
  () => props.form.transportTypeId,
  () => {
    if (internal.value)
      Object.assign(props.form, {
        privateName: "",
        relationship: "",
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
        >Cédula *<input
          v-model.trim="form.privateId"
          maxlength="11"
          inputmode="numeric"
          class="form-control"
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
