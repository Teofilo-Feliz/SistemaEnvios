<script setup>
defineProps({
  form: Object,
  errors: Object,
  locations: Array,
  destinationLocations: Array,
  flowLabel: String,
  readonlyOrigin: Boolean,
  destinoFijado: String,
});
</script>
<template>
  <div class="form-grid shipment-form-grid">
    <label
      >Origen *<select v-model="form.originId" class="form-control" :disabled="readonlyOrigin">
        <option value="">Seleccionar origen</option>
        <option
          v-for="location in locations"
          :key="location.ubicacionId"
          :value="location.ubicacionId"
        >
          {{ location.nombre }}
        </option></select
      ><small v-if="errors.origin" class="field-error">{{ errors.origin }}</small></label
    >
    <label
      >Destino *<select
        v-model="form.destinationId"
        class="form-control"
        :class="{ 'input-heredado': destinoFijado }"
        :disabled="!form.originId || Boolean(destinoFijado)"
      >
        <option value="">Seleccionar destino</option>
        <option
          v-for="location in destinationLocations"
          :key="location.ubicacionId"
          :value="location.ubicacionId"
        >
          {{ location.nombre }}
        </option></select
      ><small v-if="destinoFijado" class="field-hint"
        >El equipo tiene un caso abierto con {{ destinoFijado }} y debe volver ahí. Para mandarlo a
        otra filial hay que descartarlo primero.</small
      ><small v-else-if="errors.destination" class="field-error">{{
        errors.destination
      }}</small></label
    >
    <div v-if="form.originId" class="flow-notice span-2">
      <strong>{{ flowLabel }}</strong
      ><span>El flujo y sus estados se determinan automáticamente según origen y destino.</span>
    </div>
    <label class="span-2"
      >Observaciones<textarea v-model="form.notes" class="form-control textarea" rows="2" />
    </label>
  </div>
</template>
