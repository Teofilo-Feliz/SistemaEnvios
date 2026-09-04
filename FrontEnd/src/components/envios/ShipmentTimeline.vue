<script setup>
/**
 * Recorrido del envío. Un paso con incidencia se marca en rojo y ofrece abrirla: el color dice
 * que algo pasó y el botón lleva al detalle, sin obligar a buscarlo en otra pestaña.
 */
import { Check, Clock3, AlertTriangle } from "lucide-vue-next";

defineProps({ events: Array });
defineEmits(["ver-incidencia"]);
</script>

<template>
  <ol class="timeline">
    <li
      v-for="(event, indice) in events"
      :key="`${event.label}-${indice}`"
      :class="{ completed: event.date, current: event.current, incidencia: event.incidencia }"
    >
      <span class="timeline-marker">
        <AlertTriangle v-if="event.incidencia" :size="13" />
        <Check v-else-if="event.date" :size="13" />
        <Clock3 v-else :size="13" />
      </span>
      <div>
        <div class="timeline-encabezado">
          <strong>{{ event.label }}</strong>
          <button
            v-if="event.incidencia"
            class="timeline-ver"
            type="button"
            @click="$emit('ver-incidencia')"
          >
            Ver incidencias
          </button>
        </div>
        <time>{{ event.date || "Pendiente" }}</time>
        <small v-if="event.nota">{{ event.nota }}</small>
      </div>
    </li>
  </ol>
</template>

<style scoped>
.timeline-encabezado {
  display: flex;
  align-items: center;
  gap: 8px;
}
.timeline-ver {
  border: 1px solid #c1121f;
  background: transparent;
  color: #c1121f;
  border-radius: 4px;
  padding: 3px 9px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
  cursor: pointer;
}
.timeline-ver:hover {
  background: #c1121f;
  color: #fff;
}
/* El paso con incidencia se distingue del resto del recorrido, que va en verde. */
.timeline li.incidencia strong {
  color: #c1121f;
}
.timeline li.incidencia :deep(.timeline-marker),
.timeline li.incidencia.completed :deep(.timeline-marker) {
  background: #fde8ea;
  color: #c1121f;
  box-shadow: 0 0 0 1px #c1121f;
}
</style>
