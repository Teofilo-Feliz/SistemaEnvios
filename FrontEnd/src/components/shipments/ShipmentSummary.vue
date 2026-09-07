<script setup>
import { ClipboardList, MapPin, Route } from "lucide-vue-next";
defineProps({ form: Object, originName: String, destinationName: String, flowLabel: String });
</script>
<template>
  <aside class="shipment-summary">
    <header>Resumen</header>
    <div class="shipment-summary-body">
      <div class="summary-item">
        <Route :size="15" /><span
          ><small>Flujo operativo</small><strong>{{ flowLabel || "Por definir" }}</strong></span
        >
      </div>
      <div class="summary-item">
        <MapPin :size="15" /><span
          ><small>Origen</small><strong>{{ originName || "Sin seleccionar" }}</strong></span
        >
      </div>
      <div class="summary-item">
        <MapPin :size="15" /><span
          ><small>Destino</small><strong>{{ destinationName || "Sin seleccionar" }}</strong></span
        >
      </div>
      <div class="summary-item">
        <ClipboardList :size="15" /><span
          ><small>Equipos</small><strong>{{ form.equipment.length }}</strong></span
        >
      </div>
      <p class="form-hint">
        El envío conservará un historial auditable de todos sus cambios de estado.
      </p>
    </div>
  </aside>
</template>
