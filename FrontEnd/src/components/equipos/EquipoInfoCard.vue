<script setup>
import { computed } from "vue";
import { MapPin } from "lucide-vue-next";

// Ficha de un equipo. Se usa en el detalle del equipo y en la pestaña Equipos del envío,
// para que la información se lea igual en ambos lados.
const props = defineProps({
  equipo: { type: Object, required: true },
  types: { type: Array, default: () => [] },
  locations: { type: Array, default: () => [] },
});

const tipoNombre = computed(
  () =>
    props.types.find((x) => x.tipoEquipoId === props.equipo.tipoEquipoId)?.nombre ||
    `Tipo #${props.equipo.tipoEquipoId}`,
);
const ubicacionNombre = computed(
  () =>
    props.locations.find((x) => x.ubicacionId === props.equipo.ubicacionActualId)?.nombre ||
    `Ubicación #${props.equipo.ubicacionActualId}`,
);
const observaciones = computed(() => props.equipo.observaciones?.trim() || "");

// El código de activo y el serial son los dos datos que se leen del sticker del equipo
// para cotejarlo contra el registro, así que van arriba y en monoespaciada.
const identificadores = computed(() => [
  { label: "Código de activo", valor: props.equipo.codigoActivo },
  { label: "Número de serie", valor: props.equipo.numeroSerie },
]);
</script>

<template>
  <div class="equipo-ficha">
    <div class="equipo-placa">
      <div v-for="dato in identificadores" :key="dato.label">
        <span>{{ dato.label }}</span>
        <strong :class="{ ausente: !dato.valor }">{{ dato.valor || "Sin registrar" }}</strong>
      </div>
    </div>

    <dl class="equipo-datos">
      <div>
        <dt>Tipo</dt>
        <dd>{{ tipoNombre }}</dd>
      </div>
      <div>
        <dt>Marca</dt>
        <dd>{{ equipo.marca }}</dd>
      </div>
      <div>
        <dt>Modelo</dt>
        <dd>{{ equipo.modelo }}</dd>
      </div>
      <div>
        <dt>Registro</dt>
        <dd class="mono">#{{ equipo.equipoId }}</dd>
      </div>
    </dl>

    <p class="equipo-ubicacion">
      <MapPin :size="15" />
      <span>Ubicación actual</span>
      <strong>{{ ubicacionNombre }}</strong>
    </p>

    <div class="equipo-obs">
      <dt>Observaciones</dt>
      <dd :class="{ ausente: !observaciones }">
        {{ observaciones || "Sin observaciones registradas." }}
      </dd>
    </div>
  </div>
</template>

<style scoped>
.equipo-ficha {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

/* Los dos códigos que se leen del equipo físico para cotejarlo contra el registro. */
.equipo-placa {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 10px;
}
.equipo-placa > div {
  padding: 11px 13px;
  background: #f6f8fb;
  border: 1px solid var(--border, #e3e8eb);
  border-left: 3px solid var(--primary, #3d5f70);
  border-radius: 8px;
}
.equipo-placa span {
  display: block;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.09em;
  text-transform: uppercase;
  color: var(--muted, #6b7c87);
  margin-bottom: 5px;
}
.equipo-placa strong {
  display: block;
  font-family: ui-monospace, "Cascadia Mono", Consolas, monospace;
  font-size: 15px;
  font-weight: 600;
  letter-spacing: 0.03em;
  color: #2d3f4a;
  word-break: break-all;
}
.equipo-placa strong.ausente {
  color: var(--muted, #6b7c87);
  font-family: Inter, system-ui, sans-serif;
  font-size: 13px;
  font-weight: 400;
  font-style: italic;
  letter-spacing: 0;
}

.equipo-datos {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));
  gap: 14px 18px;
  margin: 0;
}
dt {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted, #6b7c87);
}
dd {
  margin: 3px 0 0;
  font-size: 13.5px;
  font-weight: 600;
  word-break: break-word;
}
.mono {
  font-family: ui-monospace, "Cascadia Mono", Consolas, monospace;
  font-weight: 500;
}

.equipo-ubicacion {
  display: flex;
  align-items: center;
  gap: 9px;
  margin: 0;
  padding: 11px 13px;
  border: 1px solid var(--border, #e3e8eb);
  border-radius: 8px;
  background: #f8fafc;
}
.equipo-ubicacion svg {
  color: var(--primary, #3d5f70);
  flex: none;
}
.equipo-ubicacion span {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted, #6b7c87);
}
.equipo-ubicacion strong {
  margin-left: auto;
  font-size: 13.5px;
  font-weight: 600;
  text-align: right;
}

.equipo-obs dd {
  font-size: 13px;
  font-weight: 400;
  line-height: 1.6;
  white-space: pre-line;
}
.ausente {
  color: var(--muted, #6b7c87);
  font-weight: 400;
  font-style: italic;
}
</style>
